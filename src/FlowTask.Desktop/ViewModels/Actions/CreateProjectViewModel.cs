using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.Appearance;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 创建项目：校验名称、落库并触发列表重载（TR-1）。
/// </summary>
public sealed class CreateProjectViewModel
{
    private readonly IProjectRepository _projectRepository;
    private readonly IClock _clock;

    public CreateProjectViewModel(IProjectRepository projectRepository, IClock clock)
    {
        _projectRepository = projectRepository;
        _clock = clock;
    }

    /// <summary>
    /// 名称非法时回调可展示文案（来自 <see cref="ProjectName.Validate"/>）；成功时清空输入并收起创建区。
    /// </summary>
    public async Task ExecuteAsync(
        string name,
        int sortOrder,
        Action clearAndCollapse,
        Func<Task> reloadProjects,
        Action<string> onInvalid)
    {
        var error = ProjectName.Validate(name);
        if (error is not null)
        {
            onInvalid(error);
            return;
        }

        var project = new Project
        {
            Name = ProjectName.Normalize(name),
            SortOrder = sortOrder,
            ColorHex = AppearanceCoordinator.PickPaletteColor(sortOrder),
            CreatedAt = _clock.UtcNow
        };

        await _projectRepository.SaveProjectAsync(project);
        clearAndCollapse();
        await reloadProjects();
    }

    /// <summary>
    /// 离开输入框时提交：空白视为放弃（收起、不提示）；非空则走 <see cref="ExecuteAsync"/>。
    /// </summary>
    public Task ExecuteOnLeaveAsync(
        string name,
        int sortOrder,
        Action collapse,
        Func<Task> reloadProjects,
        Action<string> onInvalid)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            collapse();
            return Task.CompletedTask;
        }

        return ExecuteAsync(name, sortOrder, collapse, reloadProjects, onInvalid);
    }
}
