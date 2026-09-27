using FlowTask.Desktop.Services;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 热键注册等待不得用 Sleep 猜时序（扫描 H4）。
/// </summary>
public class HotkeyRegistrationWaitTests
{
    [Fact]
    public void Wait_ReturnsTrueWhenRegistrationSucceedsInTime()
    {
        var tcs = new TaskCompletionSource<bool>();
        tcs.SetResult(true);

        Assert.True(HotkeyRegistrationWait.Wait(tcs, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Wait_ReturnsFalseWhenRegistrationReportsFailure()
    {
        var tcs = new TaskCompletionSource<bool>();
        tcs.SetResult(false);

        Assert.False(HotkeyRegistrationWait.Wait(tcs, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Wait_ReturnsFalseOnTimeoutWithoutCompleting()
    {
        var tcs = new TaskCompletionSource<bool>();

        Assert.False(HotkeyRegistrationWait.Wait(tcs, TimeSpan.FromMilliseconds(30)));
        Assert.False(tcs.Task.IsCompleted);

        tcs.TrySetResult(true);
        Assert.True(await tcs.Task);
    }
}
