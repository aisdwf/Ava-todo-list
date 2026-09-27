namespace FlowTask.Desktop.Services;

/// <summary>
/// 主窗关闭时要执行的效果（扫描 L8）。
/// </summary>
public enum ClosePolicyEffect
{
    HideToTray,
    Exit,
    ShowPrompt
}

/// <summary>
/// 把已保存的关闭策略分派成可测的效果，不触碰窗口。
/// </summary>
public static class ClosePolicyDispatcher
{
    public static ClosePolicyEffect Decide(CloseActionKind? stored)
        => stored switch
        {
            CloseActionKind.MinimizeToTray => ClosePolicyEffect.HideToTray,
            CloseActionKind.Exit => ClosePolicyEffect.Exit,
            _ => ClosePolicyEffect.ShowPrompt
        };
}
