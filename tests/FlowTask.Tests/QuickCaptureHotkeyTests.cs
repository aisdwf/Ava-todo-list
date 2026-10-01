using System.Reflection;
using Avalonia.Input;
using FlowTask.Desktop.Services;
using FlowTask.Desktop.Views;
using Xunit;

namespace FlowTask.Tests;

public class QuickCaptureHotkeyTests
{
    [Fact]
    public void OptionMapping_UsesAltAndDoesNotTreatCommandAsOption()
    {
        // macOS 的 Option 映射为 Alt、Command 映射为 Meta：窗内回退不能把 Command+Space 当成 Option+Space
        Assert.True(QuickWindowHotkey.Default.Matches(KeyModifiers.Alt, Key.Space));
        Assert.False(QuickWindowHotkey.Default.Matches(KeyModifiers.Meta, Key.Space));
        Assert.False(QuickWindowHotkey.Default.Matches(KeyModifiers.Alt | KeyModifiers.Meta, Key.Space));
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
