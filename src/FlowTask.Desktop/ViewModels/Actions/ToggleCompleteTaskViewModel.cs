using CommunityToolkit.Mvvm.Messaging;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Messages;
using FlowTask.Core.Models;

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

    public async Task ExecuteAsync(TaskItem? item, Func<Task> reloadTasks)
    {
        if (item is null)
        {
            return;
        }

        item.CompletedAt = item.IsCompleted ? _clock.UtcNow : null;
        await _taskRepository.SaveTaskAsync(item);
        // 单一数据源：完成态只在 SQLite。总线通知各窗口从仓储重载自己的派生列表，
        // 禁止窗口之间改对方的 TaskItem 实例（spec-cross-window-complete-sync）。
        WeakReferenceMessenger.Default.Send(new TaskSavedMessage(item));
        await reloadTasks();
    }
}
