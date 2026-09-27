using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 确认删除项目并级联物理删除其下任务（TR-1；R-2.7）。
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
        Func<Task> reloadTasks,
        object origin)
    {
        if (target is null)
        {
            return;
        }

        if (target.Id == DefaultProject.Id)
        {
            clearPending();
            return;
        }

        await _projectRepository.DeleteAsync(target.Id);
        clearPending();
        // 级联删除的任务与项目表变更分两条消息。项目消息必须后发：
        // 对端处理它时会重建下拉（选中项目已删则回退 Default）并整表重载任务，
        // 是最终状态；先发的任务刷新即使仍查旧项目，也会被加载代次丢弃。
        // TaskId 对端只用来触发整表刷新；传项目 Id 即可，避免再查一遍已删行。
        TaskChangeBus.Deleted(target.Id, origin);
        ProjectChangeBus.Changed(origin);
        await reloadProjects();

        if (!wasSelected)
        {
            await reloadTasks();
        }
    }
}
