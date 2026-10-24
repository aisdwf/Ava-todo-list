using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Infrastructure.Export;

/// <summary>
/// 从仓储读取项目与任务，写成一份 UTF-8 JSON 快照。
/// </summary>
/// <remarks>
/// 只依赖仓储接口和 <see cref="IClock"/>，不引用 Avalonia。
/// 调用方传入目标流；覆盖已有文件时的替换由桌面层负责。
/// </remarks>
public sealed class JsonDataExporter
{
    /// <summary>
    /// 写出选项。字段名、缩进和中文都不在调用方再配一份。
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly IProjectRepository _projects;
    private readonly ITaskRepository _tasks;
    private readonly IClock _clock;

    public JsonDataExporter(IProjectRepository projects, ITaskRepository tasks, IClock clock)
    {
        _projects = projects;
        _tasks = tasks;
        _clock = clock;
    }

    /// <summary>
    /// 构建快照并写入 <paramref name="destination"/>。不关闭该流。
    /// </summary>
    /// <returns>写入的项目数与任务数。</returns>
    public async Task<ExportWriteResult> WriteAsync(
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        var projects = await _projects.GetAllProjectsAsync();
        // 仓储契约已排除 IsDeleted = 1 的历史行，含已完成与已归档项目下的任务。
        var tasks = await _tasks.GetAllActiveTasksAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var document = new DataExportDocument
        {
            SchemaVersion = ExportSchema.Current,
            ExportedAt = FormatInstant(_clock.UtcNow),
            Projects = projects.Select(MapProject).ToList(),
            Tasks = tasks.Select(MapTask).ToList()
        };

        await JsonSerializer.SerializeAsync(destination, document, SerializerOptions, cancellationToken);
        return new ExportWriteResult(document.Projects.Count, document.Tasks.Count);
    }

    private static ProjectExportRecord MapProject(Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        SortOrder = project.SortOrder,
        IsArchived = project.IsArchived,
        CreatedAt = FormatInstant(project.CreatedAt)
    };

    private static TaskExportRecord MapTask(TaskItem task) => new()
    {
        Id = task.Id,
        ProjectId = task.ProjectId,
        Title = task.Title,
        Priority = task.Priority.ToString(),
        DueDate = FormatCalendarDay(task.DueDate),
        IsCompleted = task.IsCompleted,
        CompletedAt = task.CompletedAt is null ? null : FormatInstant(task.CompletedAt.Value),
        IsCliWritten = task.IsCliWritten,
        CreatedAt = FormatInstant(task.CreatedAt)
    };

    /// <summary>
    /// 时刻写成秒精度的 UTC。未标记 Kind 的值按 UTC 解释：
    /// 库存的创建/完成时刻就是 UTC，sqlite-net 读回时不保留 Kind，
    /// 若再做本地换算会把同一瞬间挪到另一天。
    /// </summary>
    private static string FormatInstant(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 到期日只取已存储的年月日。不做时区换算（design-domain-contract §3）。
    /// </summary>
    private static string? FormatCalendarDay(DateTime? dueDate)
        => dueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>一次导出写出的项目数与任务数。</summary>
public readonly record struct ExportWriteResult(int ProjectCount, int TaskCount);
