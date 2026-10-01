using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.Services;

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
        Action clearAndCollapse,
        Func<Task> reloadProjects,
        Action<string> onInvalid,
        object origin)
    {
        var error = ProjectName.Validate(name);
        if (error is not null)
        {
            onInvalid(error);
            return;
        }

        var normalized = ProjectName.Normalize(name);
        if (await _projectRepository.NameIsTakenAsync(normalized, exceptId: null))
        {
            onInvalid("项目名称已存在。");
            return;
        }

        var sortOrder = await _projectRepository.NextSortOrderAsync();
        var project = new Project
        {
            Name = normalized,
            SortOrder = sortOrder,
            CreatedAt = _clock.UtcNow
        };

        await _projectRepository.SaveProjectAsync(project);
        ProjectChangeBus.Changed(origin);
        clearAndCollapse();
        await reloadProjects();
    }

    /// <summary>
    /// 离开输入框时提交：空白视为放弃（收起、不提示）；非空则走 <see cref="ExecuteAsync"/>。
    /// </summary>
    public Task ExecuteOnLeaveAsync(
        string name,
        Action collapse,
        Func<Task> reloadProjects,
        Action<string> onInvalid,
        object origin)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            collapse();
            return Task.CompletedTask;
        }

        return ExecuteAsync(name, collapse, reloadProjects, onInvalid, origin);
    }
}
