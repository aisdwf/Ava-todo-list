namespace FlowTask.Desktop.Services;

/// <summary>
/// 等待热键消息线程给出注册结果。禁止用 Sleep 猜时序（Article 9）。
/// </summary>
public static class HotkeyRegistrationWait
{
    /// <summary>单次注册允许的上限；超时视为失败。</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 在超时内取到结果则返回它；超时则抢先把 <paramref name="timeoutResult"/> 写进同一个
    /// <see cref="TaskCompletionSource{TResult}"/> 并返回最终落定的值。
    /// </summary>
    /// <remarks>
    /// 抢写而不是只返回超时值：消息线程随后注册成功时 <c>TrySetResult</c> 会失败，
    /// 它据此立刻注销刚注册的组合。否则界面以为失败、系统里却多挂着一个热键，
    /// 与窗内回退形成双路径（扫描 H4 记录过的同类竞态）。
    /// </remarks>
    public static async Task<T> WaitAsync<T>(TaskCompletionSource<T> registration, TimeSpan timeout, T timeoutResult)
    {
        ArgumentNullException.ThrowIfNull(registration);

        try
        {
            return await registration.Task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            registration.TrySetResult(timeoutResult);
            return await registration.Task.ConfigureAwait(false);
        }
    }
}
