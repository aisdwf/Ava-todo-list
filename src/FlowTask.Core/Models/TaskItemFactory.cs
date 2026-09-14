using FlowTask.Core.Enums;
using FlowTask.Core.Interfaces;

namespace FlowTask.Core.Models;

/// <summary>
/// 任务创建的唯一入口。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要工厂</b>：主窗口与随手记小窗都会创建任务。
/// 若各自 <c>new TaskItem { ... }</c>，则「创建时间如何赋值」
/// 「标签如何规范化」等不变量会散落两处，日后必然漂移（Article 6）。
/// </para>
/// <para>
/// <b>为未来输入语法预留</b>：design-domain-contract §3.2 已认可 <c>#项目 @标签</c>
/// 内联语法为正确方向。届时解析逻辑只需接入本工厂一处，
/// 而不必在每个创建点重复。这是把它收敛为单一入口的主要动机。
/// </para>
/// <para>
/// <b>命名说明</b>：不叫 <c>TaskFactory</c> 是为避开与
/// <see cref="System.Threading.Tasks.TaskFactory"/> 的名称冲突 ——
/// 在启用 <c>ImplicitUsings</c> 的项目中该冲突会迫使每个调用点写全限定名。
/// 此处以领域类型名 <c>TaskItem</c> 为前缀，同时更准确表达它生产的是何种实体。
/// </para>
/// </remarks>
public static class TaskItemFactory
{
    /// <summary>
    /// 创建一个新任务。
    /// </summary>
    /// <param name="clock">时间提供者，用于显式赋值创建时刻。</param>
    /// <param name="title">标题。调用方须自行确保非空白（空白标题应在 UI 层静默忽略）。</param>
    /// <param name="priority">优先级，默认中优先级。</param>
    /// <param name="projectId">所属项目；<c>null</c> 表示未归属，这是正常默认状态。</param>
    /// <param name="tags">标签序列，内部会经 <see cref="TagNormalizer"/> 规范化。</param>
    /// <param name="dueDate">到期日；仓储会将其归一化为日历日。</param>
    /// <param name="description">补充说明。</param>
    public static TaskItem Create(
        IClock clock,
        string title,
        TaskPriority priority = TaskPriority.Medium,
        string? projectId = null,
        IEnumerable<string>? tags = null,
        DateTime? dueDate = null,
        string? description = null)
        => new()
        {
            // 经 TaskTitle 规范化：仅修剪首尾，内部字符原样保留 ——
            // 未来输入语法依赖 '#' / '@' 等字符不被提前处理掉（design-domain-contract §3.2）
            Title = TaskTitle.Normalize(title),
            Priority = priority,
            ProjectId = projectId,
            Tags = TagNormalizer.Normalize(tags),
            DueDate = dueDate,
            Description = description,
            CreatedAt = clock.UtcNow
        };
}
