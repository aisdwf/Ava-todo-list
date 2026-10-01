using FlowTask.Desktop.Services;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 关闭策略分派抽出后可单测（扫描 L8）。
/// </summary>
public class ClosePolicyDispatcherTests
{
    [Fact]
    public void Decide_Tray_Hides()
        => Assert.Equal(ClosePolicyEffect.HideToTray, ClosePolicyDispatcher.Decide(CloseActionKind.MinimizeToTray));

    [Fact]
    public void Decide_Exit_Exits()
        => Assert.Equal(ClosePolicyEffect.Exit, ClosePolicyDispatcher.Decide(CloseActionKind.Exit));

    [Fact]
    public void Decide_Unset_ShowsPrompt()
        => Assert.Equal(ClosePolicyEffect.ShowPrompt, ClosePolicyDispatcher.Decide(null));
}
