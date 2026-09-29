using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 到期日选择器浮层的编辑状态：快捷预设 / 日历 / 纯数字三来源同步（design-interaction-principles §4.3）。
/// </summary>
/// <remarks>
/// <para>
/// <b>预览与提交分离</b>（spec-due-date-picker）：数字输入与日历方向键只改变
/// <see cref="CalendarDate"/>（浮层内的高亮预览），不改变 <see cref="SelectedDate"/>。
/// 只有「提交类」操作——数字框按 Enter、点击日历某天、「默认 +N 天」、「清除」——
/// 才写入 <see cref="SelectedDate"/> 并触发 <see cref="Committed"/>。
/// 此前逐键解析即生效，输入 <c>10</c> 时会先被当成 1 日提交。
/// </para>
/// <para>
/// 编辑器本身不落库：创建栏只读取 <see cref="TakeValue"/>，
/// 任务行由持有者订阅 <see cref="Committed"/> 后持久化。
/// </para>
/// </remarks>
public partial class DueDateEditorViewModel : ViewModelBase
{
    private readonly IClock _clock;
    private readonly Func<int> _getOffsetDays;
    private bool _syncing;

    /// <summary>已提交的到期日；<c>null</c> 表示未设置。</summary>
    [ObservableProperty]
    private DateTime? _selectedDate;

    /// <summary>数字快速输入框文本。</summary>
    [ObservableProperty]
    private string _digitText = string.Empty;

    /// <summary>浮层内日历的高亮日期（预览态）。</summary>
    [ObservableProperty]
    private DateTime? _calendarDate;

    /// <summary>日历当前显示的月份，预览跨月时随之翻页。</summary>
    [ObservableProperty]
    private DateTime _calendarDisplayDate;

    [ObservableProperty]
    private bool _hasParseError;

    [ObservableProperty]
    private string _parseErrorMessage = string.Empty;

    /// <summary>
    /// 某次提交完成。参数为提交后的到期日（可为 <c>null</c>）。视图据此关闭浮层，持有者据此落库。
    /// </summary>
    public event Action<DateTime?>? Committed;

    public DueDateEditorViewModel(IClock clock, Func<int> getOffsetDays)
    {
        _clock = clock;
        _getOffsetDays = getOffsetDays;
        _calendarDisplayDate = clock.Today;
    }

    /// <summary>「默认 +N 天」按钮文案；N 来自设置，打开浮层时经 <see cref="Load"/> 刷新。</summary>
    public string DefaultPresetLabel => $"默认 +{_getOffsetDays()} 天";

    /// <summary>
    /// 以指定日期开始一次编辑会话：已提交值与预览都回到该日期，清空输入与错误。
    /// </summary>
    public void Load(DateTime? initialDate)
    {
        _syncing = true;
        try
        {
            SelectedDate = initialDate;
            CalendarDate = initialDate;
            CalendarDisplayDate = initialDate ?? _clock.Today;
            DigitText = string.Empty;
            ClearError();
        }
        finally
        {
            _syncing = false;
        }

        OnPropertyChanged(nameof(DefaultPresetLabel));
    }

    /// <summary>提交「今天 + N 天」。</summary>
    [RelayCommand]
    private void ApplyDefaultDue() => CommitDate(_clock.Today.AddDays(_getOffsetDays()));

    /// <summary>提交清除到期日。</summary>
    [RelayCommand]
    private void ClearDue() => CommitDate(null);

    /// <summary>
    /// 提交当前预览（数字框 Enter / 日历上按 Enter）。数字非法时不提交，保留提示。
    /// </summary>
    [RelayCommand]
    private void CommitPreview()
    {
        if (HasParseError)
        {
            return;
        }

        CommitDate(CalendarDate);
    }

    /// <summary>
    /// 提交指定日期（日历点击某天、预设、清除的统一出口）。
    /// </summary>
    public void CommitDate(DateTime? date)
    {
        _syncing = true;
        try
        {
            SelectedDate = date;
            CalendarDate = date;
            if (date is { } d)
            {
                CalendarDisplayDate = d;
            }

            DigitText = string.Empty;
            ClearError();
        }
        finally
        {
            _syncing = false;
        }

        Committed?.Invoke(date);
    }

    /// <summary>
    /// 数字输入的预览解析：合法则移动日历高亮，非法则保留原预览并提示；均不提交。
    /// </summary>
    public void UpdateDigitInput(string? newText)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            DigitText = newText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newText))
            {
                ClearError();
                return;
            }

            var result = DueDateParser.TryParse(newText, _clock.Today);
            if (result.Status == DueDateParser.ParseStatus.Success && result.Date is { } date)
            {
                CalendarDate = date;
                CalendarDisplayDate = date;
                ClearError();
            }
            else
            {
                HasParseError = true;
                ParseErrorMessage = "无法识别，试试 10、0310 或 20260310";
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>已提交的到期日。</summary>
    public DateTime? TakeValue() => SelectedDate;

    partial void OnDigitTextChanged(string value) => UpdateDigitInput(value);

    /// <summary>
    /// 日历高亮被用户移动（方向键）时，数字框里的旧文本已不代表当前预览，清空以免误导。
    /// </summary>
    partial void OnCalendarDateChanged(DateTime? value)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            DigitText = string.Empty;
            ClearError();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ClearError()
    {
        HasParseError = false;
        ParseErrorMessage = string.Empty;
    }
}
