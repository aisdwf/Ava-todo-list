using System.Globalization;
using Avalonia.Data.Converters;
using FlowTask.Core.Enums;

namespace FlowTask.Desktop.Converters;

/// <summary>
/// 将 <see cref="TaskPriority"/> 投影为 Editorial 微型标签文案 (SPEC-0003 §2)。
/// </summary>
/// <remarks>
/// 此处只产出文本。标签配色刻意不在转换器内解析为笔刷：若返回具体
/// <c>IBrush</c> 实例，昼夜切换时该实例不会随主题字典重新求值，标签会保留旧主题色。
/// 配色改由 <see cref="PriorityMatchConverter"/> 驱动样式类，在样式层以
/// DynamicResource 表达，从而天然跟随主题变体（Article 6）。
/// </remarks>
public sealed class PriorityTagConverter : IValueConverter
{
    /// <summary>微型标签文案，如 P1。</summary>
    public static PriorityTagConverter Tag { get; } = new(showDisplayName: false);

    /// <summary>中文可读名称，用于无障碍提示与工具提示。</summary>
    public static PriorityTagConverter DisplayName { get; } = new(showDisplayName: true);

    private readonly bool _showDisplayName;

    private PriorityTagConverter(bool showDisplayName) => _showDisplayName = showDisplayName;

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TaskPriority priority)
        {
            return null;
        }

        if (_showDisplayName)
        {
            return priority switch
            {
                TaskPriority.High => "高优先级",
                TaskPriority.Medium => "中优先级",
                _ => "低优先级"
            };
        }

        return priority switch
        {
            TaskPriority.High => "P1",
            TaskPriority.Medium => "P2",
            _ => "P3"
        };
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// 标签为单向展示投影；允许反向写入会让绑定错误被静默吞掉。
    /// </exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("PriorityTagConverter 仅支持单向绑定。");
}

/// <summary>
/// 判定任务优先级是否等于指定档位，用于条件性样式类绑定
/// （<c>Classes.P1="{Binding Priority, Converter=..., ConverterParameter=High}"</c>）。
/// </summary>
public sealed class PriorityMatchConverter : IValueConverter
{
    /// <summary>共享实例，转换器无状态可安全复用。</summary>
    public static PriorityMatchConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TaskPriority priority
           && parameter is string expected
           && Enum.TryParse<TaskPriority>(expected, ignoreCase: true, out var target)
           && priority == target;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">样式类绑定为单向。</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("PriorityMatchConverter 仅支持单向绑定。");
}
