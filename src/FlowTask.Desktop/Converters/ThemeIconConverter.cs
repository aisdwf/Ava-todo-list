using System.Globalization;
using Avalonia.Data.Converters;

namespace FlowTask.Desktop.Converters;

/// <summary>
/// 昼夜切换按钮图标：深色主题显示太阳（提示可切换至明亮），浅色主题显示月亮。
/// </summary>
/// <remarks>
/// 为什么用转换器而非 ViewModel 字符串属性：图标纯粹由 <c>IsDarkTheme</c> 派生，
/// 原实现另设 <c>ThemeIcon</c> 可变字段，需要在每个主题切换分支手工同步，
/// 属于可被推导的冗余状态（Article 6 / Article 10）。
/// </remarks>
public sealed class ThemeIconConverter : IValueConverter
{
    /// <summary>共享实例，转换器无状态可安全复用。</summary>
    public static ThemeIconConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "☀" : "☾";

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">图标为单向派生展示。</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("ThemeIconConverter 仅支持单向绑定。");
}
