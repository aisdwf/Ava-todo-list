using FlowTask.Core.Models;

namespace FlowTask.Core.Interfaces;

/// <summary>
/// 任务仓储契约。
/// </summary>
public interface ITaskRepository
{
    Task<List<TaskItem>> GetAllActiveTasksAsync();

    /// <summary>
    /// 载入「今日聚焦」：到期日为今天**或已逾期**的未完成任务。
    /// </summary>
    /// <remarks>
    /// 含逾期项是有意设计。若严格只取今天，昨天到期而未完成的任务
    /// 会同时从「今日」和视觉焦点中消失，成为注意力盲区（design-domain-contract §5.4）。
    /// </remarks>
    Task<List<TaskItem>> GetTodayTasksAsync();

    Task<List<TaskItem>> GetCompletedTasksAsync();

    /// <summary>
    /// 按项目载入未完成任务。
    /// </summary>
    /// <param name="projectId">
    /// 目标项目；传 <c>null</c> 时返回**未归属任何项目**的任务，
    /// 而非全部任务 —— 「未归属」本身是一个有意义的筛选维度。
    /// </param>
    Task<List<TaskItem>> GetTasksByProjectAsync(string? projectId);

    /// <summary>
    /// 按标签载入未完成任务（大小写不敏感）。
    /// </summary>
    /// <remarks>
    /// 标签以分隔符拼接存储，故此处为内存过滤而非索引查询。
    /// 该代价在设计阶段已明确接受（design-domain-contract §2.3）。
    /// </remarks>
    Task<List<TaskItem>> GetTasksByTagAsync(string tag);

    /// <summary>
    /// 提取全部在用标签，去重后按字母序返回。
    /// </summary>
    /// <remarks>
    /// 用于标签输入时的自动补全建议 —— 这是缓解「拼写不一致产生近似重复标签」
    /// 的主要手段（design-domain-contract §2.3）。
    /// </remarks>
    Task<List<string>> GetAllTagsAsync();

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
}
