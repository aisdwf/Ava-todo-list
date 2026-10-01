using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.Services;

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
        Action<string> syncSelectedTitle,
        object origin)
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

        var normalized = ProjectName.Normalize(project.RenameBuffer);
        if (await _projectRepository.NameIsTakenAsync(normalized, project.Id))
        {
            project.CancelRename();
            return;
        }

        var previousName = project.Project.Name;
        project.Project.Name = ProjectName.Normalize(project.RenameBuffer);
        try
        {
            await _projectRepository.SaveProjectAsync(project.Project);
        }
        catch
        {
            project.Project.Name = previousName;
            throw;
        }

        ProjectChangeBus.Changed(origin);
        project.SyncFromEntity();
        project.CancelRename();

        if (selectedProjectId == project.Id)
        {
            syncSelectedTitle(project.Name);
        }
    }
}
