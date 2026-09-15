using SQLite;

namespace FlowTask.Core.Models;

/// <summary>
/// 受管理的标签实体。
/// </summary>
[Table("Tags")]
public class Tag
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Indexed]
    public string Name { get; set; } = string.Empty;

    /// <summary>标签色值，格式为 <c>#RRGGBB</c>。</summary>
    public string ColorHex { get; set; } = "#3B82F6";

    /// <summary>设置页中的展示顺序。</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否由内置目录提供。内置标签同样允许删除；此字段只记录来源。
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>创建时刻（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}
