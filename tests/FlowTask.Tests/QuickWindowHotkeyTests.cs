using Avalonia.Input;
using FlowTask.Desktop.Services;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 快捷小窗快捷键模型（spec-quick-window-custom-hotkey）：解析、规范存值、校验、Win32 映射。
/// </summary>
public class QuickWindowHotkeyTests
{
    [Theory]
    [InlineData("Alt+Space", "Alt+Space")]
    [InlineData("ctrl+alt+k", "Ctrl+Alt+K")]
    [InlineData(" Win + Shift + F5 ", "Shift+Win+F5")]
    [InlineData("Alt+Ctrl+7", "Ctrl+Alt+7")]
    [InlineData("Ctrl+F12", "Ctrl+F12")]
    public void TryParse_AcceptsValidAndCanonicalizes(string raw, string expected)
    {
        Assert.True(QuickWindowHotkey.TryParse(raw, out var hotkey));
        Assert.Equal(expected, hotkey.ToStorage());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Space")]           // 无修饰键
    [InlineData("Shift+K")]         // Shift 不算必需修饰键
    [InlineData("Ctrl+Enter")]      // 主键不在支持范围
    [InlineData("Ctrl+F13")]
    [InlineData("Hyper+K")]         // 未知修饰键
    [InlineData("Ctrl++K")]
    [InlineData("Ctrl+C")]          // 保留组合：手改库也不能注册
    public void TryParse_RejectsBadValuesAndFallsBackToDefault(string? raw)
    {
        Assert.False(QuickWindowHotkey.TryParse(raw, out var hotkey));
        Assert.Equal(QuickWindowHotkey.Default, hotkey);
    }

    [Fact]
    public void RoundTrip_PreservesEveryAllowedKeyFamily()
    {
        foreach (var key in new[] { Key.Space, Key.A, Key.Z, Key.D0, Key.D9, Key.F1, Key.F12 })
        {
            var hotkey = new QuickWindowHotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, key);
            Assert.True(QuickWindowHotkey.TryParse(hotkey.ToStorage(), out var parsed), hotkey.ToStorage());
            Assert.Equal(hotkey, parsed);
        }
    }

    [Theory]
    [InlineData(HotkeyModifiers.Ctrl, Key.C)]
    [InlineData(HotkeyModifiers.Ctrl, Key.V)]
    [InlineData(HotkeyModifiers.Ctrl, Key.X)]
    [InlineData(HotkeyModifiers.Ctrl, Key.Z)]
    [InlineData(HotkeyModifiers.Ctrl, Key.Y)]
    [InlineData(HotkeyModifiers.Ctrl, Key.A)]
    [InlineData(HotkeyModifiers.Ctrl, Key.S)]
    [InlineData(HotkeyModifiers.Ctrl, Key.D)]
    [InlineData(HotkeyModifiers.Ctrl, Key.Tab)]
    [InlineData(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, Key.Tab)]
    [InlineData(HotkeyModifiers.Alt, Key.Tab)]
    [InlineData(HotkeyModifiers.Alt, Key.F4)]
    public void Validate_RejectsReservedCombinations(HotkeyModifiers modifiers, Key key)
    {
        var hotkey = new QuickWindowHotkey(modifiers, key);
        Assert.Equal(HotkeyValidation.Reserved, hotkey.Validate());
        Assert.Contains("常用", hotkey.DescribeRejection());
    }

    [Fact]
    public void Validate_ExplainsMissingModifierAndUnsupportedKey()
    {
        Assert.Equal(HotkeyValidation.NeedsModifier, new QuickWindowHotkey(HotkeyModifiers.Shift, Key.K).Validate());
        Assert.Equal(HotkeyValidation.UnsupportedKey, new QuickWindowHotkey(HotkeyModifiers.Ctrl, Key.Enter).Validate());
        Assert.Null(QuickWindowHotkey.Default.DescribeRejection());
    }

    [Fact]
    public void FromKeyEvent_WaitsWhileOnlyModifiersArePressed()
    {
        Assert.Null(QuickWindowHotkey.FromKeyEvent(KeyModifiers.Control, Key.LeftCtrl));
        Assert.Null(QuickWindowHotkey.FromKeyEvent(KeyModifiers.Alt, Key.RightAlt));
        Assert.Null(QuickWindowHotkey.FromKeyEvent(KeyModifiers.Meta, Key.LWin));

        Assert.Equal(
            new QuickWindowHotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win, Key.K),
            QuickWindowHotkey.FromKeyEvent(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta, Key.K));
    }

    [Theory]
    [InlineData("Alt+Space", 0x0001u, 0x20u)]
    [InlineData("Ctrl+Alt+K", 0x0003u, 0x4Bu)]
    [InlineData("Shift+Win+F5", 0x000Cu, 0x74u)]
    [InlineData("Ctrl+0", 0x0002u, 0x30u)]
    public void Win32Mapping_MatchesRegisterHotKeyConstants(string raw, uint modifiers, uint virtualKey)
    {
        Assert.True(QuickWindowHotkey.TryParse(raw, out var hotkey));
        Assert.Equal(modifiers, hotkey.Win32Modifiers);
        Assert.Equal(virtualKey, hotkey.Win32VirtualKey);
    }

    [Fact]
    public void Matches_RequiresExactModifiers()
    {
        Assert.True(QuickWindowHotkey.TryParse("Ctrl+Alt+K", out var hotkey));

        Assert.True(hotkey.Matches(KeyModifiers.Control | KeyModifiers.Alt, Key.K));
        Assert.False(hotkey.Matches(KeyModifiers.Control, Key.K));
        Assert.False(hotkey.Matches(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, Key.K));
    }
}
