namespace FlowTask.Infrastructure.Export;

/// <summary>
/// 一份数据导出的根对象。属性集合是导出字段的唯一清单（Article 6）。
/// </summary>
/// <remarks>
/// 时刻写成 UTC 文本、到期日写成日历日文本，避免序列化器再做一次时区换算。
/// 不含设置、遗留列、软删行，也不含任务行上的展示文案。
/// <c>isPinned</c> / <c>pinnedAt</c> 在置顶合入后补上：导出先落地时这两列还不在 dev。
/// </remarks>
public sealed class DataExportDocument
{
    /// <summary>格式版本，取值只来自 <see cref="ExportSchema.Current"/>。</summary>
    public int SchemaVersion { get; init; } = ExportSchema.Current;

    /// <summary>导出时刻，UTC，形如 <c>2026-10-01T08:00:00Z</c>。</summary>
    public required string ExportedAt { get; init; }

    /// <summary>全部项目，含已归档与 Default。</summary>
    public required IReadOnlyList<ProjectExportRecord> Projects { get; init; }

    /// <summary>全部未删除任务，含已完成，以及已归档项目下的任务。</summary>
    public required IReadOnlyList<TaskExportRecord> Tasks { get; init; }
}

/// <summary>导出文件中的一个项目。</summary>
public sealed class ProjectExportRecord
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required int SortOrder { get; init; }

    public required bool IsArchived { get; init; }

    /// <summary>创建时刻，UTC，秒精度，<c>Z</c> 结尾。</summary>
    public required string CreatedAt { get; init; }
}

/// <summary>
/// 导出文件中的一条任务。
/// </summary>
/// <remarks>
/// <c>priority</c> 写枚举名而不是整数，避免以后改枚举值时旧文件语义漂移。
/// <c>isCliWritten</c> 是库里的布尔值，不是任务行上的「CLI」二字。
/// </remarks>
public sealed class TaskExportRecord
{
    public required string Id { get; init; }

    public string? ProjectId { get; init; }

    public required string Title { get; init; }

    public required string Priority { get; init; }

    /// <summary>日历日 <c>yyyy-MM-dd</c>；没有到期日时为 <c>null</c>。</summary>
    public string? DueDate { get; init; }

    public required bool IsCompleted { get; init; }

    /// <summary>完成时刻，UTC；未完成时为 <c>null</c>。</summary>
    public string? CompletedAt { get; init; }

    public required bool IsCliWritten { get; init; }

    public required bool IsPinned { get; init; }

    /// <summary>最近一次置顶的时刻，UTC；未置顶时为 <c>null</c>。</summary>
    public string? PinnedAt { get; init; }

    /// <summary>创建时刻，UTC，秒精度，<c>Z</c> 结尾。</summary>
    public required string CreatedAt { get; init; }
}
