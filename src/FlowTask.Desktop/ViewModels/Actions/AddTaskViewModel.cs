using FlowTask.Core.Enums;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 新增任务：工厂创建、标签替换、输入复位与列表刷新（TR-1）。
/// </summary>
public sealed class AddTaskViewModel
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IClock _clock;

    public AddTaskViewModel(
        ITaskRepository taskRepository,
        ITagRepository tagRepository,
        IClock clock)
    {
        _taskRepository = taskRepository;
        _tagRepository = tagRepository;
        _clock = clock;
    }

    public async Task ExecuteAsync(
        string title,
        TaskPriority priority,
        DateTime? dueDate,
        IEnumerable<string> selectedTagIds,
        Action resetInput,
        bool leaveCompletedView,
        Action switchToActiveView,
        Func<Task> reloadTasks)
    {
        if (!TaskTitle.IsValid(title))
        {
            return;
        }

        var task = TaskItemFactory.Create(_clock, title, priority, dueDate: dueDate);
        await _taskRepository.SaveTaskAsync(task);
        await _tagRepository.ReplaceTaskTagsAsync(task.Id, selectedTagIds);
        resetInput();

        if (leaveCompletedView)
        {
            switchToActiveView();
        }

        await reloadTasks();
    }
}
