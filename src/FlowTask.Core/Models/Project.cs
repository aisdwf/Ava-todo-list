using SQLite;

namespace FlowTask.Core.Models;

/// <summary>
/// 项目实体：任务的分类归属。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么项目是独立实体而标签不是</b>：项目需要重命名
/// （改一次、全部关联任务同步生效）、需要颜色标识、需要排序与归档。
/// 这些都要求它拥有独立身份。若退化为字符串标记，
/// 重命名将变成「批量查找替换」，且无从承载颜色与排序（DESIGN-0004 §2.2）。
/// </para>
/// <para>
/// 反之标签只需「附着」，不需治理能力，故以字符串存储即可。
/// </para>
/// </remarks>
[Table("Projects")]
public class Project
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Indexed]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 项目色（`#RRGGBB`），用于任务行色条与筛选标识。
    /// </summary>
    public string ColorHex { get; set; } = "#3B82F6";

    /// <summary>侧边栏中的排列顺序，升序。</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否已归档。
    /// </summary>
    /// <remarks>
    /// 归档与删除语义不同，二者并存：
    /// <list type="bullet">
    ///   <item><description>
    ///   <b>归档</b>：项目已完成、不再活跃，但历史归属有保留价值。
    ///   其下任务的 <c>ProjectId</c> 保持不变，仍可显示归属。
    ///   </description></item>
    ///   <item><description>
    ///   <b>删除</b>：项目建错或不再需要，归属信息无价值。
    ///   其下任务的 <c>ProjectId</c> 置 <c>null</c>，**任务本身永不被删除**。
    ///   </description></item>
    /// </list>
    /// 项目本身无软删除标记：那会引出「项目回收站」，
    /// 而任务不被销毁已消除删除操作的主要风险，再建完整的删除-恢复生命周期属过度设计。
    /// </remarks>
    public bool IsArchived { get; set; }

    /// <summary>创建时刻（UTC）。</summary>
    /// <remarks>与 <see cref="TaskItem.CreatedAt"/> 同理，由创建方经 <c>IClock</c> 显式赋值。</remarks>
    public DateTime CreatedAt { get; set; }
}
