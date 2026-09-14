using FlowTask.Core.Enums;
using FlowTask.Core.Models;
using FlowTask.Desktop.Converters;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 覆盖 Editorial 视觉层依赖的绑定转换器。
/// </summary>
/// <remarks>
/// 为什么需要这组测试：SPEC-0003 的优先级微标签与主题图标完全由转换器驱动，
/// 而绑定转换失败在 Avalonia 中只会静默产出空白，既不报编译错也不抛异常。
/// 缺少断言时，标签消失这类回归无法被机器发现（Article 1 测试疏漏）。
/// </remarks>
public class ConverterTests
{
    [Theory]
    [InlineData(TaskPriority.High, "P1")]
    [InlineData(TaskPriority.Medium, "P2")]
    [InlineData(TaskPriority.Low, "P3")]
    public void PriorityTag_MapsEveryPriorityToLabel(TaskPriority priority, string expected)
    {
        var actual = PriorityTagConverter.Tag.Convert(priority, typeof(string), null, null!);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PriorityTag_ReturnsNullForNonPriorityInput()
    {
        // 绑定在数据未就绪时会传入 null，此路径必须安全降级而非抛出
        Assert.Null(PriorityTagConverter.Tag.Convert(null, typeof(string), null, null!));
    }

    [Theory]
    [InlineData(TaskPriority.High, "High", true)]
    [InlineData(TaskPriority.High, "Low", false)]
    [InlineData(TaskPriority.Low, "low", true)]
    public void PriorityMatch_ComparesAgainstParameter(TaskPriority priority, string parameter, bool expected)
    {
        var actual = PriorityMatchConverter.Instance.Convert(priority, typeof(bool), parameter, null!);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(TaskPriority.Medium, "Medium", true)]
    [InlineData(TaskPriority.Medium, "High", false)]
    public void EnumChoice_ReflectsCurrentSelection(TaskPriority priority, string parameter, bool expected)
    {
        var actual = EnumChoiceConverter.Instance.Convert(priority, typeof(bool), parameter, null!);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumChoice_WritesBackSelectedEnumValue()
    {
        var actual = EnumChoiceConverter.Instance.ConvertBack(true, typeof(TaskPriority), "High", null!);

        Assert.Equal(TaskPriority.High, actual);
    }

    [Fact]
    public void EnumChoice_IgnoresDeselection()
    {
        // 单选组切换时旧项会先收到 false，回写会覆盖新值，因此必须不做任何事
        var actual = EnumChoiceConverter.Instance.ConvertBack(false, typeof(TaskPriority), "High", null!);

        Assert.Equal(Avalonia.Data.BindingOperations.DoNothing, actual);
    }

    [Fact]
    public void ThemeIcon_ShowsSunInDarkAndMoonInLight()
    {
        Assert.Equal("☀", ThemeIconConverter.Instance.Convert(true, typeof(string), null, null!));
        Assert.Equal("☾", ThemeIconConverter.Instance.Convert(false, typeof(string), null, null!));
    }
}
