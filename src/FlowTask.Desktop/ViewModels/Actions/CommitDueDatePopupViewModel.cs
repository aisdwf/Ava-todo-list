using FlowTask.Core.Interfaces;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 保存到期日弹层更改并关闭（TR-1）。
/// </summary>
public sealed class CommitDueDatePopupViewModel
{
    private readonly ITaskRepository _taskRepository;

    public CommitDueDatePopupViewModel(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task ExecuteAsync(
        TaskRowViewModel? row,
        DateTime? dueDate,
        Action closePopup,
        Func<Task> reloadTasks)
    {
        if (row is null)
        {
            closePopup();
            return;
        }

        row.Task.DueDate = dueDate;
        await _taskRepository.SaveTaskAsync(row.Task);
        await reloadTasks();
        closePopup();
    }
}
