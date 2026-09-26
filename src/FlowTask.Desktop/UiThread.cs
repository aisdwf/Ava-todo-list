using Avalonia.Threading;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 把跨窗消息的续延拉回 UI 线程。仓储 await 之后可能落到线程池，
/// 在那边改 <c>ObservableCollection</c> 时 Avalonia 不会重绘。
/// </summary>
internal static class UiThread
{
    public static Task RunAsync(Func<Task> work)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return work();
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await work().ConfigureAwait(true);
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }
}
