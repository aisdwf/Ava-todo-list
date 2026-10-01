using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 持久化任务行上优先级浮层的一次点选（TR-1，spec-inline-task-edit）。
/// </summary>
/// <remarks>
/// 与 <see cref="CommitRowDueDateViewModel"/> 同构：先写实体再落库，失败回滚；
/// 值未变时直接返回。落库后重载，排序按新优先级刷新（TaskListOrder）。
/// </remarks>
public sealed class CommitRowPriorityViewModel
{
    private readonly ITaskRepository _taskRepository;

    public CommitRowPriorityViewModel(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    /// <param name="commit">浮层提交；<see cref="PriorityCommit.Target"/> 须为任务行。</param>
    /// <param name="reloadTasks">落库成功后的列表刷新。</param>
    /// <param name="origin">总线消息来源，供本窗口跳过自身广播。</param>
    public async Task ExecuteAsync(PriorityCommit? commit, Func<Task> reloadTasks, object origin)
    {
        if (commit?.Target is not TaskRowViewModel row || row.Task.Priority == commit.Priority)
        {
            return;
        }

        var previous = row.Task.Priority;
        row.Task.Priority = commit.Priority;
        try
        {
            await _taskRepository.SaveTaskAsync(row.Task);
        }
        catch
        {
            row.Task.Priority = previous;
            throw;
        }

        TaskChangeBus.Saved(row.Task, origin);
        row.RefreshDerivedFlags();
        await reloadTasks();
    }
}
