using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 到期日编辑器；支持快捷预设、日历、纯数字输入三来源同步。
/// </summary>
public partial class DueDateEditorViewModel : ViewModelBase
{
    private readonly IClock _clock;
    private readonly Func<int> _getOffsetDays;
    private bool _syncing;

    [ObservableProperty]
    private DateTime? _selectedDate;

    [ObservableProperty]
    private string _digitText = string.Empty;

    [ObservableProperty]
    private DateTime? _calendarDate;

    [ObservableProperty]
    private bool _hasParseError;

    [ObservableProperty]
    private string _parseErrorMessage = string.Empty;

    /// <summary>
    /// 日历是否展开。创建条默认收起，避免常驻占位；弹出设期时可展开。
    /// </summary>
    [ObservableProperty]
    private bool _isCalendarExpanded;

    public DueDateEditorViewModel(IClock clock, Func<int> getOffsetDays)
    {
        _clock = clock;
        _getOffsetDays = getOffsetDays;
    }

    /// <summary>
    /// 初始化编辑器，加载指定的初始日期。
    /// </summary>
    /// <param name="initialDate">初始到期日。</param>
    /// <param name="expandCalendar">是否展开日历（弹出设期为 <c>true</c>，创建条为 <c>false</c>）。</param>
    public void Load(DateTime? initialDate, bool expandCalendar = false)
    {
        ApplyState(initialDate, clearDigit: true, clearError: true);
        IsCalendarExpanded = expandCalendar;
    }

    [RelayCommand]
    private void ToggleCalendar() => IsCalendarExpanded = !IsCalendarExpanded;

    [RelayCommand]
    private void EnableDefaultDue()
    {
        var dueDate = _clock.Today.AddDays(_getOffsetDays());
        ApplyState(dueDate, clearDigit: true, clearError: true);
    }

    // 命令名 EnableDefaultDueCommand，与既有测试一致

    [RelayCommand]
    private void ClearDue()
    {
        ApplyState(null, clearDigit: true, clearError: true);
    }

    /// <summary>
    /// 供测试与命令显式调用的数字解析入口。
    /// </summary>
    public void UpdateDigitInput(string? newText)
    {
        if (_syncing)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(newText))
        {
            _syncing = true;
            DigitText = string.Empty;
            HasParseError = false;
            ParseErrorMessage = string.Empty;
            _syncing = false;
            return;
        }

        var result = DueDateParser.TryParse(newText, _clock.Today);
        _syncing = true;
        try
        {
            DigitText = newText;
            switch (result.Status)
            {
                case DueDateParser.ParseStatus.Success:
                    SelectedDate = result.Date;
                    CalendarDate = result.Date;
                    HasParseError = false;
                    ParseErrorMessage = string.Empty;
                    break;
                case DueDateParser.ParseStatus.Invalid:
                    HasParseError = true;
                    ParseErrorMessage = "日期格式无效，请使用 10、0310 或 20260310 格式";
                    break;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// 供测试与命令显式调用的日历入口。
    /// </summary>
    public void UpdateCalendarInput(DateTime? newDate)
    {
        ApplyState(newDate, clearDigit: true, clearError: true);
    }

    public DateTime? TakeValue() => SelectedDate;

    partial void OnDigitTextChanged(string value)
    {
        if (_syncing)
        {
            return;
        }

        UpdateDigitInput(value);
    }

    partial void OnCalendarDateChanged(DateTime? value)
    {
        if (_syncing || value == SelectedDate)
        {
            return;
        }

        UpdateCalendarInput(value);
    }

    private void ApplyState(DateTime? date, bool clearDigit, bool clearError)
    {
        _syncing = true;
        try
        {
            SelectedDate = date;
            CalendarDate = date;
            if (clearDigit)
            {
                DigitText = string.Empty;
            }

            if (clearError)
            {
                HasParseError = false;
                ParseErrorMessage = string.Empty;
            }
        }
        finally
        {
            _syncing = false;
        }
    }
}
