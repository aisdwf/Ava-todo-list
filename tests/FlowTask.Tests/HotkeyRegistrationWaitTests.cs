using FlowTask.Desktop.Services;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 热键注册等待不得用 Sleep 猜时序（扫描 H4）；超时必须抢占结果，迟到的成功才能被注销。
/// </summary>
public class HotkeyRegistrationWaitTests
{
    [Fact]
    public async Task WaitAsync_ReturnsResultWhenRegistrationCompletesInTime()
    {
        var tcs = new TaskCompletionSource<bool>();
        tcs.SetResult(true);

        Assert.True(await HotkeyRegistrationWait.WaitAsync(tcs, TimeSpan.FromSeconds(1), false));
    }

    [Fact]
    public async Task WaitAsync_ReturnsFailureReportedByRegistration()
    {
        var tcs = new TaskCompletionSource<HotkeyRegistrationOutcome>();
        tcs.SetResult(HotkeyRegistrationOutcome.Occupied);

        Assert.Equal(
            HotkeyRegistrationOutcome.Occupied,
            await HotkeyRegistrationWait.WaitAsync(tcs, TimeSpan.FromSeconds(1), HotkeyRegistrationOutcome.Failed));
    }

    [Fact]
    public async Task WaitAsync_TimeoutClaimsResultSoLateSuccessIsRejected()
    {
        var tcs = new TaskCompletionSource<HotkeyRegistrationOutcome>();

        var outcome = await HotkeyRegistrationWait.WaitAsync(
            tcs, TimeSpan.FromMilliseconds(30), HotkeyRegistrationOutcome.Failed);

        Assert.Equal(HotkeyRegistrationOutcome.Failed, outcome);
        // 消息线程迟到的 TrySetResult 必须失败，它据此注销刚注册的组合
        Assert.False(tcs.TrySetResult(HotkeyRegistrationOutcome.Registered));
    }
}
