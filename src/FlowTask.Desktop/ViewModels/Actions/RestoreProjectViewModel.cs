using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 恢复已归档项目。排序值未改，重载后回到侧边栏原位置（TR-1）。
/// </summary>
public sealed class RestoreProjectViewModel
{
    private readonly IProjectRepository _projectRepository;

    public RestoreProjectViewModel(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task ExecuteAsync(
        ProjectItemViewModel? project,
        Func<Task> reloadProjects,
        Func<Task> reloadTasks,
        object origin)
    {
        if (project is null)
        {
            return;
        }

        await _projectRepository.SetArchivedAsync(project.Id, false);
        ProjectChangeBus.Changed(origin);
        await reloadProjects();
        await reloadTasks();
    }
}
