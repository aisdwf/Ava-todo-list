using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

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

    public async Task ExecuteAsync(TaskRowViewModel? row, Func<Task> reloadTasks)
    {
        if (row is null)
        {
            return;
        }

        var task = row.Task;

        if (TaskTitle.IsValid(row.EditTitle))
        {
            task.Title = TaskTitle.Normalize(row.EditTitle);
        }

        task.Priority = row.EditPriority;

        // 归档项目不在活跃候选时，下拉可能落到 None。未改归属不得把已有 ProjectId 写成 null。
        if (row.EditProject.ProjectId is not null || task.ProjectId is null)
        {
            task.ProjectId = row.EditProject.ProjectId;
        }

        await _taskRepository.SaveTaskAsync(task);

        row.EndEdit();
        await reloadTasks();
    }
}
