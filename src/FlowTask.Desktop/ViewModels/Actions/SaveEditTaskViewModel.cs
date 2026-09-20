using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 提交行编辑缓冲并收起面板（TR-1）。
/// </summary>
public sealed class SaveEditTaskViewModel
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITagRepository _tagRepository;

    public SaveEditTaskViewModel(ITaskRepository taskRepository, ITagRepository tagRepository)
    {
        _taskRepository = taskRepository;
        _tagRepository = tagRepository;
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
        task.ProjectId = row.EditProject.ProjectId;

        await _taskRepository.SaveTaskAsync(task);
        await _tagRepository.ReplaceTaskTagsAsync(
            task.Id,
            row.EditTagChoices
                .Where(choice => choice.IsSelected)
                .Select(choice => choice.Tag.Id));

        row.EndEdit();
        await reloadTasks();
    }
}
