using System.Globalization;
using Avalonia.Data.Converters;

namespace FlowTask.Desktop.Converters;

/// <summary>
/// 将枚举属性双向映射为单选按钮的勾选状态。
/// </summary>
/// <remarks>
/// 用途：优先级分段选择器需要三个 RadioButton 共同表达一个 <c>TaskPriority</c> 属性。
/// 转换参数给出该按钮代表的枚举名，勾选时把对应枚举值回写到源属性。
/// 取消勾选不回写，因为单选组内取消必然伴随另一项被选中，回写会产生竞态。
/// </remarks>
public sealed class EnumChoiceConverter : IValueConverter
{
    /// <summary>共享实例，转换器无状态可安全复用。</summary>
    public static EnumChoiceConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null
           && parameter is string expected
           && string.Equals(value.ToString(), expected, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 仅在被选中时回写，避免单选组切换过程中的取消事件覆盖新值
        if (value is not true || parameter is not string expected)
        {
            return Avalonia.Data.BindingOperations.DoNothing;
        }

        var enumType = targetType;
        if (Nullable.GetUnderlyingType(enumType) is { } underlying)
        {
            enumType = underlying;
        }

        if (!enumType.IsEnum || !Enum.TryParse(enumType, expected, ignoreCase: true, out var parsed))
        {
            return Avalonia.Data.BindingOperations.DoNothing;
        }

        return parsed;
    }
}
