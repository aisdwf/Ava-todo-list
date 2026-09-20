using FlowTask.Core.Interfaces;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 确认删除项目并将任务改挂 Default（TR-1）。
/// </summary>
public sealed class ConfirmDeleteProjectViewModel
{
    private readonly IProjectRepository _projectRepository;

    public ConfirmDeleteProjectViewModel(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task ExecuteAsync(
        ProjectItemViewModel? target,
        bool wasSelected,
        Action clearPending,
        Func<Task> reloadProjects,
        Func<Task> reloadTasks)
    {
        if (target is null)
        {
            return;
        }

        await _projectRepository.DeleteAsync(target.Id);
        clearPending();
        await reloadProjects();

        if (!wasSelected)
        {
            await reloadTasks();
        }
    }
}
