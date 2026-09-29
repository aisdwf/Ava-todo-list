using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowTask.Core.Interfaces;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 到期日选择器浮层的编辑状态：快捷预设 + 日历（design-interaction-principles §4.3）。
/// </summary>
/// <remarks>
/// <para>
/// <b>预览与提交分离</b>（spec-due-date-picker）：日历方向键只改变
/// <see cref="CalendarDate"/>（浮层内的高亮预览），不改变 <see cref="SelectedDate"/>。
/// 只有「提交类」操作——点击日历某天、日历上按 Enter、「默认 +N 天」、「清除」——
/// 才写入 <see cref="SelectedDate"/> 并触发 <see cref="Committed"/>。
/// </para>
/// <para>
/// <b>为什么没有数字输入</b>：用户原话（2026-09-29）「目前的操作逻辑下我认为不需要硬编码日期了，
/// 我考虑删掉直接输入日期的形式」。贴边日历一键可达后，数字速记成了第二种要记规则的录入方式。
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

    /// <summary>已提交的到期日；<c>null</c> 表示未设置。</summary>
    [ObservableProperty]
    private DateTime? _selectedDate;

    /// <summary>浮层内日历的高亮日期（预览态）。</summary>
    [ObservableProperty]
    private DateTime? _calendarDate;

    /// <summary>日历当前显示的月份。</summary>
    [ObservableProperty]
    private DateTime _calendarDisplayDate;

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
    /// 以指定日期开始一次编辑会话：已提交值与预览都回到该日期。
    /// </summary>
    public void Load(DateTime? initialDate)
    {
        SelectedDate = initialDate;
        CalendarDate = initialDate;
        CalendarDisplayDate = initialDate ?? _clock.Today;
        OnPropertyChanged(nameof(DefaultPresetLabel));
    }

    /// <summary>提交「今天 + N 天」。</summary>
    [RelayCommand]
    private void ApplyDefaultDue() => CommitDate(_clock.Today.AddDays(_getOffsetDays()));

    /// <summary>提交清除到期日。</summary>
    [RelayCommand]
    private void ClearDue() => CommitDate(null);

    /// <summary>
    /// 提交当前高亮（日历上按 Enter）。尚未高亮任何日期时不提交，避免误清空已有日期。
    /// </summary>
    [RelayCommand]
    private void CommitPreview()
    {
        if (CalendarDate is { } date)
        {
            CommitDate(date);
        }
    }

    /// <summary>
    /// 提交指定日期（日历点击某天、预设、清除的统一出口）。
    /// </summary>
    public void CommitDate(DateTime? date)
    {
        SelectedDate = date;
        CalendarDate = date;
        if (date is { } d)
        {
            CalendarDisplayDate = d;
        }

        Committed?.Invoke(date);
    }

    /// <summary>已提交的到期日。</summary>
    public DateTime? TakeValue() => SelectedDate;
}
