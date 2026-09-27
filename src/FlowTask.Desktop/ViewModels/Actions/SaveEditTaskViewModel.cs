using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 提交行编辑缓冲并收起面板（TR-1）。
/// </summary>
public sealed class SaveEditTaskViewModel
{
    private readonly ITaskRepository _taskRepository;

    public SaveEditTaskViewModel(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task ExecuteAsync(TaskRowViewModel? row, Func<Task> reloadTasks, object origin)
    {
        if (row is null)
        {
            return;
        }

        var task = row.Task;
        var previousTitle = task.Title;
        var previousPriority = task.Priority;
        var previousProjectId = task.ProjectId;

        if (TaskTitle.IsValid(row.EditTitle))
        {
            task.Title = TaskTitle.Normalize(row.EditTitle);
        }

        task.Priority = row.EditPriority;
        task.ProjectId = row.EditProject.ProjectId;

        try
        {
            await _taskRepository.SaveTaskAsync(task);
        }
        catch
        {
            task.Title = previousTitle;
            task.Priority = previousPriority;
            task.ProjectId = previousProjectId;
            throw;
        }

        TaskChangeBus.Saved(task, origin);

        row.EndEdit();
        await reloadTasks();
    }
}
