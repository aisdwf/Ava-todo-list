namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 切换 VIEWS 筛选；同值不重载（TR-1）。
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
        closeSettings();

        var target = filter switch
        {
            TaskFilter.Completed => ViewSelection.Completed,
            _ => ViewSelection.Active
        };

        if (current == target)
        {
            return;
        }

        applySelection(target);
        reloadTasksFireAndForget();
    }
}
