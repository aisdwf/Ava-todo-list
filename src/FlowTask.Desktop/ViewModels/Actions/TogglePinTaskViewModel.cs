using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 切换未完成任务的置顶，并刷新列表（TR-1）。主窗与小窗共用。
/// </summary>
public sealed class TogglePinTaskViewModel
{
    private readonly ITaskRepository _taskRepository;
    private readonly IClock _clock;

    public TogglePinTaskViewModel(ITaskRepository taskRepository, IClock clock)
    {
        _taskRepository = taskRepository;
        _clock = clock;
    }

    public async Task ExecuteAsync(TaskItem? item, Func<Task> reloadTasks, object origin)
    {
        if (item is null || item.IsCompleted)
        {
            return;
        }

        var previousPinned = item.IsPinned;
        var previousPinnedAt = item.PinnedAt;
        if (item.IsPinned)
        {
            item.IsPinned = false;
            item.PinnedAt = null;
        }
        else
        {
            item.IsPinned = true;
            item.PinnedAt = _clock.UtcNow;
        }

        try
        {
            await _taskRepository.SaveTaskAsync(item);
        }
        catch
        {
            item.IsPinned = previousPinned;
            item.PinnedAt = previousPinnedAt;
            throw;
        }

        TaskChangeBus.Saved(item, origin);
        await reloadTasks();
    }
}
