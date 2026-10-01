using System.Reflection;
using Avalonia.Input;
using FlowTask.Desktop.Views;
using Xunit;

namespace FlowTask.Tests;

public class QuickCaptureHotkeyTests
{
    [Fact]
    public void OptionMapping_UsesAltAndDoesNotTreatCommandAsOption()
    {
        var method = typeof(MainWindow).GetMethod(
            "IsQuickCaptureModifier",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.True((bool)method!.Invoke(null, [KeyModifiers.Alt])!);
        Assert.False((bool)method.Invoke(null, [KeyModifiers.Meta])!);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void BackgroundQuickCapture_HidesMainWindowOnlyWhenMacOSAppWasInactive(
        bool applicationWasActive,
        bool isMacOS,
        bool expected)
    {
        var method = typeof(MainWindow).GetMethod(
            "ShouldHideMainWindowBeforeQuickCapture",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.Equal(
            expected,
            (bool)method!.Invoke(null, [applicationWasActive, true, isMacOS])!);
    }
}
