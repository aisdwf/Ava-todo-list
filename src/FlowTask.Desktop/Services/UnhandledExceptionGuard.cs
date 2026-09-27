using Avalonia.Threading;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 注册进程级未处理异常入口，全部写入 <see cref="AppLog"/>。
/// </summary>
public static class UnhandledExceptionGuard
{
    private static int _installed;

    /// <summary>幂等安装 UI / Task / AppDomain 三类处理器。</summary>
    public static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            AppLog.Write("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Write("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            AppLog.Write("Dispatcher.UnhandledException", e.Exception);
        };
    }
}
