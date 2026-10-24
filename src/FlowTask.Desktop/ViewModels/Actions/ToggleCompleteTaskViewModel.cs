using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 切换任务完成状态并刷新列表（TR-1）。
/// </summary>
public sealed class ToggleCompleteTaskViewModel
{
    private readonly ITaskRepository _taskRepository;
    private readonly IClock _clock;

    public ToggleCompleteTaskViewModel(ITaskRepository taskRepository, IClock clock)
    {
        _taskRepository = taskRepository;
        _clock = clock;
    }

    public async Task ExecuteAsync(TaskItem? item, Func<Task> reloadTasks, object origin)
    {
        if (item is null)
        {
            return;
        }

        var previousCompleted = !item.IsCompleted;
        var previousCompletedAt = item.CompletedAt;
        var previousPinned = item.IsPinned;
        var previousPinnedAt = item.PinnedAt;
        item.CompletedAt = item.IsCompleted ? _clock.UtcNow : null;
        // 勾选完成时同一次写入清掉置顶。取消勾选不恢复：置顶是临时把事情顶上去，做完即失效。
        if (item.IsCompleted)
        {
            item.IsPinned = false;
            item.PinnedAt = null;
        }

        try
        {
            await _taskRepository.SaveTaskAsync(item);
        }
        catch
        {
            item.IsCompleted = previousCompleted;
            item.CompletedAt = previousCompletedAt;
            item.IsPinned = previousPinned;
            item.PinnedAt = previousPinnedAt;
            throw;
        }
        // 单一数据源：完成态只在 SQLite。总线通知各窗口从仓储重载自己的派生列表，
        // 禁止窗口之间改对方的 TaskItem 实例（spec-cross-window-complete-sync）。
        TaskChangeBus.Saved(item, origin);
        await reloadTasks();
    }
}
