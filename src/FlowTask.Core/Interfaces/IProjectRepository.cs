using FlowTask.Core.Models;

namespace FlowTask.Core.Interfaces;

/// <summary>
/// 项目仓储契约。
/// </summary>
public interface IProjectRepository
{
    /// <summary>载入全部未归档项目，按 <see cref="Project.SortOrder"/> 升序。</summary>
    Task<List<Project>> GetActiveProjectsAsync();

    /// <summary>载入全部项目，含已归档。</summary>
    Task<List<Project>> GetAllProjectsAsync();

    Task<Project?> GetByIdAsync(string id);

    /// <summary>新增或更新项目。</summary>
    Task<int> SaveProjectAsync(Project project);

    /// <summary>设置归档状态。任务归属不受影响。</summary>
    Task<int> SetArchivedAsync(string id, bool isArchived);

    /// <summary>
    /// 删除项目，并将其下任务改挂 <see cref="DefaultProject"/>（R-2.6）。
    /// </summary>
    /// <returns>受影响的任务条数。</returns>
    /// <remarks>
    /// <b>绝不删除任务。</b>禁止删除 Default 本身。
    /// 两步操作须在单个事务内完成。
    /// </remarks>
    Task<int> DeleteAsync(string id);

    /// <summary>统计项目下未删除的任务条数，用于删除前的影响提示。</summary>
    Task<int> CountTasksAsync(string projectId);

    /// <summary>
    /// 确保 Default 项目存在，并将历史 <c>ProjectId IS NULL</c> 的任务迁到 Default。
    /// </summary>
    Task EnsureDefaultProjectAsync(DateTime createdAtUtc);
}
