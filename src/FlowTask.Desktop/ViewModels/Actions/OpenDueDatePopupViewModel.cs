namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 打开行上到期日编辑弹层（TR-1）。
/// </summary>
public sealed class OpenDueDatePopupViewModel
{
    public void Execute(
        TaskRowViewModel? row,
        Action<TaskRowViewModel> open)
    {
        if (row is null)
        {
            return;
        }

        open(row);
    }
}
