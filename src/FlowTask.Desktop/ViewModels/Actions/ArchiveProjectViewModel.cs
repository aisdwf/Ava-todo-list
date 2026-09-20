using FlowTask.Core.Interfaces;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 归档项目；未选中该项目时仍刷新任务流计数（TR-1）。
/// </summary>
public sealed class ArchiveProjectViewModel
{
    private readonly IProjectRepository _projectRepository;

    public ArchiveProjectViewModel(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task ExecuteAsync(
        ProjectItemViewModel? project,
        bool wasSelected,
        Func<Task> reloadProjects,
        Func<Task> reloadTasks)
    {
        if (project is null)
        {
            return;
        }

        await _projectRepository.SetArchivedAsync(project.Id, true);
        await reloadProjects();

        if (!wasSelected)
        {
            await reloadTasks();
        }
    }
}
