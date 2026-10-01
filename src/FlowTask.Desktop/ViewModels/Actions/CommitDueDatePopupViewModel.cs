using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;

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
        Func<Task> reloadTasks,
        object origin)
    {
        if (row is null)
        {
            closePopup();
            return;
        }

        var previousDue = row.Task.DueDate;
        row.Task.DueDate = dueDate;
        try
        {
            await _taskRepository.SaveTaskAsync(row.Task);
        }
        catch
        {
            row.Task.DueDate = previousDue;
            throw;
        }

        TaskChangeBus.Saved(row.Task, origin);
        await reloadTasks();
        closePopup();
    }
}
