using FlowTask.Core.Models;

namespace FlowTask.Core.Interfaces;

/// <summary>
/// 任务仓储契约。
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// 载入全部未删除任务（含已完成；spec-project-managed-tasks）。
    /// </summary>
    /// <remarks>
    /// 任务归档已废除：结果不再按 <c>IsArchived</c> 过滤。
    /// 排序见 <c>TaskListOrder</c>。
    /// </remarks>
    Task<List<TaskItem>> GetAllActiveTasksAsync();

    /// <summary>
    /// 载入「今日聚焦」：到期日为今天**或已逾期**的未完成任务。
    /// </summary>
    /// <remarks>
    /// 含逾期项是有意设计。若严格只取今天，昨天到期而未完成的任务
    /// 会同时从「今日」和视觉焦点中消失，成为注意力盲区（design-domain-contract §5.4）。
    /// </remarks>
    Task<List<TaskItem>> GetTodayTasksAsync();

    /// <summary>
    /// 按项目载入未删除任务（含已完成）。
    /// </summary>
    /// <param name="projectId">
    /// 目标项目；传 <c>null</c> 时返回**未归属任何项目**的任务，
    /// 而非全部任务 —— 「未归属」本身是一个有意义的筛选维度。
    /// </param>
    Task<List<TaskItem>> GetTasksByProjectAsync(string? projectId);

    Task<TaskItem?> GetByIdAsync(string id);

    /// <summary>未删除任务总数，供侧边栏「全部」计数，避免为 Count 拉整表。</summary>
    Task<int> CountActiveTasksAsync();

    /// <summary>按 <see cref="TaskItem.ProjectId"/> 分组的未删除任务数。</summary>
    Task<IReadOnlyDictionary<string, int>> CountTasksGroupedByProjectAsync();

    /// <summary>
    /// 新增或更新任务。
    /// </summary>
    /// <remarks>
    /// 这是任务写入的唯一漏斗，负责维护 <c>CreatedAt</c> 已赋值
    /// 与 <c>DueDate</c> 归一化两条不变量。
    /// </remarks>
    Task<int> SaveTaskAsync(TaskItem item);

    /// <summary>
    /// 物理删除任务行（spec-project-managed-tasks Q1=B）。
    /// </summary>
    Task<int> PermanentDeleteAsync(string id);
}
