namespace FlowTask.Desktop.Services;

/// <summary>
/// 等待后台 STA 线程完成 <c>RegisterHotKey</c>。禁止用 Sleep 猜时序（Article 9）。
/// </summary>
public static class HotkeyRegistrationWait
{
    /// <summary>冷启动允许的上限；超时则视为注册失败并应停掉消息线程。</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 在超时内取到结果则返回该布尔值；超时或故障返回 false。
    /// </summary>
    public static bool Wait(TaskCompletionSource<bool> registration, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(registration);

        try
        {
            return registration.Task.Wait(timeout) && registration.Task.Result;
        }
        catch (AggregateException)
        {
            return false;
        }
    }
}
