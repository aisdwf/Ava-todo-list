namespace FlowTask.Desktop.Services;

/// <summary>
/// 主窗关闭策略的权威取值（spec-close-to-tray）。
/// </summary>
public enum CloseActionKind
{
    /// <summary>隐藏主窗到托盘，进程继续。</summary>
    MinimizeToTray,

    /// <summary>彻底退出进程。</summary>
    Exit
}

/// <summary>
/// 关闭策略的解析与序列化。键名留在 Desktop：关窗/托盘是壳层概念，不进 Core 仓储接口。
/// </summary>
public static class CloseBehaviorCoordinator
{
    /// <summary>AppSettings 键。缺键或无法识别视为未设默认，每次主窗关闭都询问。</summary>
    public const string SettingsKey = "Window.CloseAction";

    /// <summary>最小化到托盘的存值。</summary>
    public const string TrayStorage = "tray";

    /// <summary>彻底退出的存值。</summary>
    public const string ExitStorage = "exit";

    /// <summary>
    /// 把库中的字符串变成关闭策略。空、空白、未知值一律当作未设默认，
    /// 避免坏数据把用户锁进一种关不掉或问不出的状态。
    /// </summary>
    public static CloseActionKind? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (string.Equals(raw.Trim(), TrayStorage, StringComparison.OrdinalIgnoreCase))
        {
            return CloseActionKind.MinimizeToTray;
        }

        if (string.Equals(raw.Trim(), ExitStorage, StringComparison.OrdinalIgnoreCase))
        {
            return CloseActionKind.Exit;
        }

        return null;
    }

    /// <summary>把已选择的策略写成存值。未设默认不调用本方法（不写键）。</summary>
    public static string ToStorage(CloseActionKind kind)
        => kind switch
        {
            CloseActionKind.MinimizeToTray => TrayStorage,
            CloseActionKind.Exit => ExitStorage,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown close action.")
        };
}
