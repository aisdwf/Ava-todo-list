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

    /// <summary>下一个 SortOrder：全表 MAX + 1，避免归档/删除后撞序。</summary>
    Task<int> NextSortOrderAsync();

    /// <summary>名称是否已被其它项目占用（含归档，大小写不敏感）。</summary>
    Task<bool> NameIsTakenAsync(string name, string? exceptId);

    /// <summary>设置归档状态。任务归属不受影响。</summary>
    Task<int> SetArchivedAsync(string id, bool isArchived);

    /// <summary>
    /// 删除项目，并物理删除其下全部任务（R-2.7）。
    /// </summary>
    /// <returns>被删除的任务条数（含历史 <c>IsDeleted</c> 行）。</returns>
    /// <remarks>
    /// 禁止删除 Default 本身。两步操作须在单个事务内完成。
    /// </remarks>
    Task<int> DeleteAsync(string id);

    /// <summary>统计项目下未删除的任务条数，用于删除前的影响提示。</summary>
    Task<int> CountTasksAsync(string projectId);

    /// <summary>
    /// 确保 Default 项目存在。不迁移 <c>ProjectId IS NULL</c> 的任务
    /// （那一步只在主窗启动，见 <see cref="MigrateNullProjectIdsToDefaultAsync"/>）。
    /// </summary>
    Task EnsureDefaultProjectAsync(DateTime createdAtUtc);

    /// <summary>
    /// 将历史 <c>ProjectId IS NULL</c> 的未删除任务改挂 Default（R-2.6）。
    /// 仅主窗启动路径调用，避免小窗打开时改写刚编辑过的归属。
    /// </summary>
    Task MigrateNullProjectIdsToDefaultAsync();
}
