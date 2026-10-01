using FlowTask.Desktop.Services;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 关闭策略解析：缺键、坏值必须当成「未设默认」，否则用户会被锁进问不出或退不出的状态。
/// </summary>
public class CloseBehaviorCoordinatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ask")]
    [InlineData("minimize")]
    public void Parse_UnknownOrEmpty_IsUnset(string? raw)
        => Assert.Null(CloseBehaviorCoordinator.Parse(raw));

    [Theory]
    [InlineData("tray")]
    [InlineData("TRAY")]
    [InlineData(" tray ")]
    public void Parse_TrayStorage_IsMinimizeToTray(string raw)
        => Assert.Equal(CloseActionKind.MinimizeToTray, CloseBehaviorCoordinator.Parse(raw));

    [Theory]
    [InlineData("exit")]
    [InlineData("Exit")]
    public void Parse_ExitStorage_IsExit(string raw)
        => Assert.Equal(CloseActionKind.Exit, CloseBehaviorCoordinator.Parse(raw));

    [Fact]
    public void ToStorage_RoundTripsKnownKinds()
    {
        Assert.Equal(
            CloseActionKind.MinimizeToTray,
            CloseBehaviorCoordinator.Parse(
                CloseBehaviorCoordinator.ToStorage(CloseActionKind.MinimizeToTray)));
        Assert.Equal(
            CloseActionKind.Exit,
            CloseBehaviorCoordinator.Parse(
                CloseBehaviorCoordinator.ToStorage(CloseActionKind.Exit)));
    }
}
