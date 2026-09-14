using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
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

        var today = DateTime.Today;
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
        => value is DateTime due && due.Date < DateTime.Today;

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
        => value is DateTime due && due.Date == DateTime.Today;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 将到期日格式化为可编辑的 `yyyy-MM-dd` 文本；<c>null</c> 转空串。
/// </summary>
/// <remarks>
/// 与 <see cref="DueDateTextConverter"/> 分离：编辑态需要**可解析回日期**的
/// 精确字面值，而列表展示需要**便于理解**的相对描述。
/// 用同一个转换器同时满足两种用途会导致编辑框里出现「今天到期」这类无法解析的文本。
/// </remarks>
public sealed class DueDateEditConverter : IValueConverter
{
    public static readonly DueDateEditConverter Instance = new();

    /// <summary>编辑态使用的日期格式。全应用唯一固定点。</summary>
    public const string Format = "yyyy-MM-dd";

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime due ? due.ToString(Format, CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// 严格按 <see cref="Format"/> 解析；非法输入返回 <c>null</c>（视为清除到期日）。
    /// </summary>
    /// <remarks>
    /// 用 <c>TryParseExact</c> 而非 <c>TryParse</c>：后者会接受
    /// 「3/10」「March 10」等多种形态并按当前区域文化推断，
    /// 同一串输入在不同机器上可能解析出不同日期。
    /// 严格格式让行为可预测。
    /// </remarks>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string)?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return DateTime.TryParseExact(
            text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}

/// <summary>
/// 将 `#RRGGBB` 色值字符串转为笔刷，供项目色条与色点使用。
/// </summary>
/// <remarks>
/// 项目色是用户数据（存于 <c>Project.ColorHex</c>），无法预先定义为静态资源令牌，
/// 故必须在此运行时解析。这是**不违反**「复用既有令牌」约束的例外情形 ——
/// 该约束针对的是设计系统的固定色，而非用户自定义数据。
/// </remarks>
public sealed class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
        {
            return Brushes.Transparent;
        }

        try
        {
            return new SolidColorBrush(Color.Parse(hex));
        }
        catch (FormatException)
        {
            // 用户数据可能因手工改库而非法；回退到透明而非崩溃
            return Brushes.Transparent;
        }
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 将标签字符串拆为列表，供 <c>ItemsControl</c> 逐个渲染。
/// </summary>
/// <remarks>
/// 复用 <see cref="TagNormalizer.Split"/> 而非在此另写拆分逻辑，
/// 避免读写两侧规则漂移（Article 6）。
/// </remarks>
public sealed class TagsToListConverter : IValueConverter
{
    public static readonly TagsToListConverter Instance = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => TagNormalizer.Split(value as string);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
