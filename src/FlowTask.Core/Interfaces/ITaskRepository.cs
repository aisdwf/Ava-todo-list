using FlowTask.Core.Models;

namespace FlowTask.Core.Interfaces;

/// <summary>
/// 任务仓储契约。
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// 载入活动任务：未删除且**未归档**（含已完成未归档，spec-task-complete-before-archive）。
    /// </summary>
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
    /// 载入「已完成归档」视图：未删除且**已归档**的任务。
    /// </summary>
    /// <remarks>
    /// 命名沿用历史（曾等价于 <c>IsCompleted</c>），语义已随
    /// spec-task-complete-before-archive 改为 <c>IsArchived</c> ——
    /// 完成但未归档的任务不在此列，仍留在活动列表。
    /// </remarks>
    Task<List<TaskItem>> GetCompletedTasksAsync();

    /// <summary>
    /// 按项目载入活动任务（未归档，含已完成未归档）。
    /// </summary>
    /// <param name="projectId">
    /// 目标项目；传 <c>null</c> 时返回**未归属任何项目**的任务，
    /// 而非全部任务 —— 「未归属」本身是一个有意义的筛选维度。
    /// </param>
    Task<List<TaskItem>> GetTasksByProjectAsync(string? projectId);

    Task<TaskItem?> GetByIdAsync(string id);

    /// <summary>
    /// 新增或更新任务。
    /// </summary>
    /// <remarks>
    /// 这是任务写入的唯一漏斗，负责维护 <c>CreatedAt</c> 已赋值
    /// 与 <c>DueDate</c> 归一化两条不变量。
    /// </remarks>
    Task<int> SaveTaskAsync(TaskItem item);

    Task<int> SoftDeleteAsync(string id);
    Task<int> PermanentDeleteAsync(string id);

    /// <summary>
    /// 手动归档全部已完成且未归档的任务（D3：全局范围，不按单项目拆分）。
    /// </summary>
    /// <remarks>
    /// 只处理 <c>IsCompleted &amp;&amp; !IsArchived</c> 的行（D1：未完成任务不可归档）；
    /// 归档后保留 <c>ProjectId</c> 来源，不是「整棵项目归档消失」。
    /// </remarks>
    /// <returns>受影响的行数。</returns>
    Task<int> ArchiveAllCompletedAsync();
}
