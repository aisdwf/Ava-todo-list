using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 提交项目重命名（TR-1）。
/// </summary>
public sealed class CommitRenameProjectViewModel
{
    private readonly IProjectRepository _projectRepository;

    public CommitRenameProjectViewModel(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    /// <summary>
    /// 名称非法时放弃修改；选中项目被重命名时回调刷新标题。
    /// </summary>
    public async Task ExecuteAsync(
        ProjectItemViewModel? project,
        string? selectedProjectId,
        Action<string> syncSelectedTitle)
    {
        if (project is null)
        {
            return;
        }

        if (!ProjectName.IsValid(project.RenameBuffer))
        {
            project.CancelRename();
            return;
        }

        project.Project.Name = ProjectName.Normalize(project.RenameBuffer);
        await _projectRepository.SaveProjectAsync(project.Project);

        project.SyncFromEntity();
        project.CancelRename();

        if (selectedProjectId == project.Id)
        {
            syncSelectedTitle(project.Name);
        }
    }
}
