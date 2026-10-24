using FlowTask.Core.Models;

namespace FlowTask.Core.Interfaces;

/// <summary>
/// 任务仓储契约。
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// 载入「全部任务」看板：未删除、且不属于已归档项目的任务（含已完成）。
    /// </summary>
    /// <remarks>
    /// 任务级归档已废除，不再按 <c>TaskItem.IsArchived</c> 过滤。
    /// 项目归档则把该项目下的任务移出看板与计数（spec-project-archive）。
    /// 排序见 <c>TaskListOrder</c>。
    /// 命令行 <c>list</c> 不走本方法，见 <see cref="GetAllNonDeletedTasksAsync"/>。
    /// </remarks>
    Task<List<TaskItem>> GetAllActiveTasksAsync();

    /// <summary>
    /// 载入全部未删除任务，包含已归档项目下的任务。
    /// </summary>
    /// <remarks>
    /// 供一次显式的数据读取（命令行 <c>list</c>）。
    /// 归档只隐藏侧边栏、「全部任务」和计数、小窗项目下拉，不隐藏这次读取。
    /// </remarks>
    Task<List<TaskItem>> GetAllNonDeletedTasksAsync();

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

    /// <summary>
    /// 「全部任务」计数：未删除、且不属于已归档项目。与 <see cref="GetAllActiveTasksAsync"/> 同一范围。
    /// </summary>
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
