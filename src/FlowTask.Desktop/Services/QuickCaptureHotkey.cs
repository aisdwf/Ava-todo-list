namespace FlowTask.Desktop.Services;

/// <summary>
/// 进程级热键实际注册到的组合（<see cref="GlobalHotkeyService"/>）。
/// </summary>
public enum RegisteredHotkey
{
    /// <summary>未注册：非 Windows，或注册失败，走窗内 KeyDown 回退。</summary>
    None,

    /// <summary>Alt+Space。</summary>
    AltSpace,

    /// <summary>Alt+Space 被占用后的回退 Win+Alt+Space。</summary>
    WinAltSpace
}

/// <summary>
/// 小窗热键的界面文案。
/// </summary>
/// <remarks>
/// 此前文案写死 Alt+Space：注册回退到 Win+Alt+Space 时，侧栏键帽与操作指南都会教错按键
/// （code wrong，spec-onboarding-guide Q3）。文案必须取实际注册结果。
/// </remarks>
public static class QuickCaptureHotkey
{
    /// <summary>按当前平台描述。</summary>
    public static string Describe(RegisteredHotkey registered)
        => Describe(registered, OperatingSystem.IsMacOS());

    /// <summary>按指定平台描述，供测试注入平台。</summary>
    public static string Describe(RegisteredHotkey registered, bool isMacOS)
        => registered switch
        {
            RegisteredHotkey.WinAltSpace => "Win+Alt+Space",
            RegisteredHotkey.AltSpace => "Alt+Space",
            // 窗内回退：macOS 为 Option(Meta)+Space，Windows 为 Alt+Space（MainWindow.IsQuickCaptureModifier）
            _ => isMacOS ? "⌥ Space" : "Alt+Space"
        };
}
