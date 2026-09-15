using SQLite;

namespace FlowTask.Core.Models;

/// <summary>
/// 任务与标签的多对多关联。
/// </summary>
[Table("TaskTags")]
public class TaskTag
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Indexed]
    public string TaskId { get; set; } = string.Empty;

    [Indexed]
    public string TagId { get; set; } = string.Empty;
}
