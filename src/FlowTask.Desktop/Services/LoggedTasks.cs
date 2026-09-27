namespace FlowTask.Desktop.Services;

/// <summary>
/// 观察或丢弃异步任务时记下故障，避免 fire-and-forget 把异常吞进 GC。
/// </summary>
public static class LoggedTasks
{
    /// <summary>
    /// 附加仅在故障时运行的续延并返回原任务，供仍需 <c>await</c> 的调用方使用。
    /// </summary>
    public static Task Observe(Task task, string context)
    {
        ArgumentNullException.ThrowIfNull(task);

        _ = task.ContinueWith(
            t =>
            {
                if (t.Exception is not null)
                {
                    AppLog.Write(context, t.Exception.GetBaseException());
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return task;
    }

    /// <summary>明确丢弃结果，但故障仍写入 <see cref="AppLog"/>。</summary>
    public static void FireAndForget(Task task, string context) => Observe(task, context);
}
