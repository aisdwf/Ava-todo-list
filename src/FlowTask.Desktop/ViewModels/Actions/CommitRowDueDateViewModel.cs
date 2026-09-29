using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 持久化任务行上到期日选择器的一次提交（TR-1）。主窗与小窗共用。
/// </summary>
/// <remarks>
/// 先写实体再落库，失败回滚实体，避免界面显示未落库的值（PersistThenWriteback）。
/// </remarks>
public sealed class CommitRowDueDateViewModel
{
    private readonly ITaskRepository _taskRepository;

    public CommitRowDueDateViewModel(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    /// <param name="commit">选择器提交；<see cref="DueDateCommit.Target"/> 须为任务行。</param>
    /// <param name="reloadTasks">落库成功后的列表刷新。</param>
    /// <param name="origin">总线消息来源，供本窗口跳过自身广播。</param>
    public async Task ExecuteAsync(DueDateCommit? commit, Func<Task> reloadTasks, object origin)
    {
        if (commit?.Target is not TaskRowViewModel row || row.Task.DueDate == commit.Date)
        {
            return;
        }

        var previousDue = row.Task.DueDate;
        row.Task.DueDate = commit.Date;
        try
        {
            await _taskRepository.SaveTaskAsync(row.Task);
        }
        catch
        {
            row.Task.DueDate = previousDue;
            throw;
        }

        TaskChangeBus.Saved(row.Task, origin);
        await reloadTasks();
    }
}
