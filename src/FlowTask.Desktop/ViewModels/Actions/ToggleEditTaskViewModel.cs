namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 展开或收起行编辑；展开前先提交其他编辑行（TR-1）。
/// </summary>
public sealed class ToggleEditTaskViewModel
{
    private readonly SaveEditTaskViewModel _saveEdit;

    public ToggleEditTaskViewModel(SaveEditTaskViewModel saveEdit)
    {
        _saveEdit = saveEdit;
    }

    public async Task ExecuteAsync(
        TaskRowViewModel? row,
        IEnumerable<TaskRowViewModel> allRows,
        IEnumerable<ProjectChoice> projectChoices,
        Func<Task> reloadTasks)
    {
        if (row is null)
        {
            return;
        }

        if (row.IsEditing)
        {
            await _saveEdit.ExecuteAsync(row, reloadTasks);
            return;
        }

        foreach (var other in allRows.Where(r => r.IsEditing && r != row).ToList())
        {
            await _saveEdit.ExecuteAsync(other, reloadTasks);
        }

        row.BeginEdit(projectChoices);
    }
}
