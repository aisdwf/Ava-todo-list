using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 软删除任务并刷新列表（TR-1）。
/// </summary>
public sealed class DeleteTaskViewModel
{
    private readonly ITaskRepository _taskRepository;

    public DeleteTaskViewModel(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task ExecuteAsync(TaskItem? item, Func<Task> reloadTasks)
    {
        if (item is null)
        {
            return;
        }

        await _taskRepository.SoftDeleteAsync(item.Id);
        await reloadTasks();
    }
}
