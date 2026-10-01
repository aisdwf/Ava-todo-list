namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 双击标题进入就地编辑；进入前先提交其他正在编辑的行（TR-1，spec-inline-task-edit）。
/// </summary>
/// <remarks>
/// 同一时刻只允许一行处于标题编辑：多个输入框同时打开时，
/// 「点外面保存」无法判断该提交哪一行。
/// </remarks>
public sealed class BeginRowTitleEditViewModel
{
    private readonly CommitRowTitleViewModel _commitTitle;

    public BeginRowTitleEditViewModel(CommitRowTitleViewModel commitTitle)
    {
        _commitTitle = commitTitle;
    }

    public async Task ExecuteAsync(
        TaskRowViewModel? row,
        IEnumerable<TaskRowViewModel> allRows,
        Func<Task> reloadTasks,
        object origin)
    {
        if (row is null || row.IsEditingTitle)
        {
            return;
        }

        var rows = allRows as IReadOnlyCollection<TaskRowViewModel> ?? allRows.ToList();
        foreach (var other in rows.Where(r => r.IsEditingTitle).ToList())
        {
            await _commitTitle.ExecuteAsync(other, reloadTasks, origin);
        }

        // 提交别的行会整表重载、行实例全部重建；按 Id 取回当前实例，
        // 否则编辑态落在已不在列表里的旧实例上，界面没有任何反应。
        var current = rows.FirstOrDefault(r => r.Task.Id == row.Task.Id) ?? row;
        current.BeginTitleEdit();
    }
}
