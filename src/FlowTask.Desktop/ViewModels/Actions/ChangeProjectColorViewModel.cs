using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Appearance;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 循环切换项目色并刷新任务流行色条（TR-1）。
/// </summary>
public sealed class ChangeProjectColorViewModel
{
    private readonly IProjectRepository _projectRepository;

    public ChangeProjectColorViewModel(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task ExecuteAsync(ProjectItemViewModel? project, Func<Task> reloadTasks)
    {
        if (project is null)
        {
            return;
        }

        project.Project.ColorHex = AppearanceCoordinator.CyclePaletteColor(project.ColorHex);
        await _projectRepository.SaveProjectAsync(project.Project);
        project.SyncFromEntity();
        await reloadTasks();
    }
}
