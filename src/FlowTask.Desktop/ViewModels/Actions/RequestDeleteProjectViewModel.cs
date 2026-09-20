using FlowTask.Core.Interfaces;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 查询删除影响范围并交由界面确认（TR-1）。
/// </summary>
public sealed class RequestDeleteProjectViewModel
{
    private readonly IProjectRepository _projectRepository;

    public RequestDeleteProjectViewModel(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task ExecuteAsync(
        ProjectItemViewModel? project,
        Action<ProjectItemViewModel, int> setPendingDeletion)
    {
        if (project is null)
        {
            return;
        }

        var count = await _projectRepository.CountTasksAsync(project.Id);
        setPendingDeletion(project, count);
    }
}
