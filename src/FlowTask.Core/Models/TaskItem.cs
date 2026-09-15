using SQLite;
using FlowTask.Core.Enums;

namespace FlowTask.Core.Models;

/// <summary>
/// 待办任务实体。
/// </summary>
[Table("Tasks")]
public class TaskItem
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Indexed]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 补充说明。当前无 UI 读写路径，属显式登记的待办事项（见 spec-task-contract-and-clock §6）。
    /// </summary>
    public string? Description { get; set; }

    public bool IsCompleted { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>
    /// 所属项目 Id；<c>null</c> 表示未归属任何项目。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>null</c> 是**默认且完全正常**的状态，不是「数据不完整」——
    /// 零必填是本产品的最高原则，UI 不得对其做任何催促或标记（design-domain-contract §1、§3.3）。
    /// </para>
    /// <para>
    /// 一个任务只能属于一个项目；标签维度由 <c>TaskTags</c> 关联表独立承担。
    /// </para>
    /// <para>
    /// 项目被删除时此字段置 <c>null</c>（任务本身永不随项目删除），
    /// 归档时保持不变。见 <c>IProjectRepository.DeleteAsync</c>。
    /// </para>
    /// </remarks>
    [Indexed]
    public string? ProjectId { get; set; }

    /// <summary>
    /// 到期日（deadline）。语义为「用户日历上的哪一天」，不含有意义的时间部分。
    /// </summary>
    /// <remarks>
    /// 存**本地日历日**（当日零点、Kind 为 Unspecified），而非 UTC 时刻。
    /// 若按 UTC 存储，用户在 UTC+8 设定的「今天」跨时区或夏令时变更后会漂移到前后一天 ——
    /// 这是同类应用最常见的用户可见缺陷。
    /// 归一化由 <c>SqliteTaskRepository.SaveTaskAsync</c> 统一负责。
    /// </remarks>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// 创建时刻（UTC）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么没有默认值初始化器</b>：原实现为
    /// <c>= DateTime.UtcNow</c>，使业务时间戳依赖实体字段初始化器这一隐式副作用，
    /// 且构成对系统时钟的直接依赖（Article 9）。
    /// 现由创建方经 <c>IClock</c> 显式赋值，令「任务何时创建」成为被明确决定的业务事实。
    /// </para>
    /// <para>
    /// 代价是「忘记赋值」从不可能变为可能，故由
    /// <c>SqliteTaskRepository.SaveTaskAsync</c> 断言非 <c>default</c> 并快速失败。
    /// 推荐经 <c>TaskItemFactory.Create</c> 创建，它已处理此赋值。
    /// </para>
    /// </remarks>
    public DateTime CreatedAt { get; set; }

    /// <summary>完成时刻（UTC）；未完成时为 <c>null</c>。</summary>
    public DateTime? CompletedAt { get; set; }

    public bool IsDeleted { get; set; }
}
