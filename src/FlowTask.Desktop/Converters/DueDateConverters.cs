using System.Globalization;
using Avalonia.Data.Converters;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.Converters;

/// <summary>
/// 到期日展示文案。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么日期文案不用简单的 StringFormat</b>：用户关心的不是「2026-03-10」
/// 这个字面值，而是「还剩多久 / 是否已经过期」。
/// 「今天到期」比日期本身更能驱动行动。
/// </para>
/// <para>
/// <b>此处的时间比较是展示层的相对描述，不构成业务分支</b>，
/// 故未经 <c>IClock</c> 注入。业务判断（哪些任务属于「今日聚焦」）
/// 已在仓储层由注入时钟决定（Article 9）。
/// 若日后需要测试这些文案本身，须改为可注入形式。
/// </para>
/// </remarks>
public sealed class DueDateTextConverter : IValueConverter
{
    public static readonly DueDateTextConverter Instance = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime due)
        {
            return string.Empty;
        }

        var today = DueDateDisplay.Today().Date;
        var days = (due.Date - today).Days;

        return days switch
        {
            < -1 => $"已逾期 {-days} 天",
            -1 => "昨天到期",
            0 => "今天到期",
            1 => "明天到期",
            < 7 => $"{days} 天后",
            _ => due.ToString("MM/dd", CultureInfo.InvariantCulture)
        };
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("到期日文案为单向展示，不支持反向解析。");
}

/// <summary>
/// 到期日是否已逾期，用于驱动 <c>Overdue</c> 样式类。
/// </summary>
public sealed class DueDateOverdueConverter : IValueConverter
{
    public static readonly DueDateOverdueConverter Instance = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime due && due.Date < DueDateDisplay.Today().Date;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 到期日是否为今天，用于驱动 <c>DueToday</c> 样式类。
/// </summary>
public sealed class DueDateTodayConverter : IValueConverter
{
    public static readonly DueDateTodayConverter Instance = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime due && due.Date == DueDateDisplay.Today().Date;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
