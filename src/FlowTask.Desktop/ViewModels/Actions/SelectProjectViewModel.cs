namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 选中项目或回到全部任务视图（TR-1）。
/// </summary>
public sealed class SelectProjectViewModel
{
    public async Task ExecuteAsync(
        ProjectItemViewModel? project,
        Action closeSettings,
        Func<Task> returnToActive,
        Action<string> selectProject,
        Func<Task> reloadTasks)
    {
        closeSettings();

        if (project is null)
        {
            await returnToActive();
            return;
        }

        selectProject(project.Id);
        await reloadTasks();
    }
}
