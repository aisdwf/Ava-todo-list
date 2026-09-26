namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 切换到全部任务看板；同值不重载（TR-1）。
/// </summary>
public sealed class ChangeFilterViewModel
{
    public void Execute(
        TaskFilter filter,
        ViewSelection current,
        Action closeSettings,
        Action<ViewSelection> applySelection,
        Action reloadTasksFireAndForget)
    {
        _ = filter;
        closeSettings();

        var target = ViewSelection.Active;
        if (current == target)
        {
            return;
        }

        applySelection(target);
        reloadTasksFireAndForget();
    }
}
