using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 物理删除任务并刷新列表（TR-1；spec-project-managed-tasks Q1=B）。
/// </summary>
public sealed class DeleteTaskViewModel
{
    private readonly ITaskRepository _taskRepository;

    public DeleteTaskViewModel(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task ExecuteAsync(TaskItem? item, Func<Task> reloadTasks, object origin)
    {
        if (item is null)
        {
            return;
        }

        await _taskRepository.PermanentDeleteAsync(item.Id);
        TaskChangeBus.Deleted(item.Id, origin);
        await reloadTasks();
    }
}
