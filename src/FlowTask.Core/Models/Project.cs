using SQLite;

namespace FlowTask.Core.Models;

/// <summary>
/// 项目实体：任务的分类归属。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么项目是独立实体而标签不是</b>：项目需要重命名
/// （改一次、全部关联任务同步生效）、需要排序与归档。
/// 这些都要求它拥有独立身份。若退化为字符串标记，
/// 重命名将变成「批量查找替换」，且无从承载排序（design-domain-contract §2.2）。
/// </para>
/// <para>
/// <b>为什么没有项目色</b>：曾有 <c>ColorHex</c> 字段，建项目时把字面 hex 写进库，
/// 主题系统无法介入，换主题后项目点仍是固定紫 / 绿。现在项目标识统一取主题强调色，
/// 旧库遗留的 <c>ColorHex</c> 列不再读写（spec-theme-bound-decoration-colors）。
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
    ///   <b>删除</b>：项目建错或不再需要。其下任务一并物理删除（R-2.7）。
    ///   </description></item>
    /// </list>
    /// 项目本身无软删除标记：那会引出「项目回收站」。
    /// 不可逆删除的闸门是确认条，不是把任务改挂到 Default。
    /// </remarks>
    public bool IsArchived { get; set; }

    /// <summary>创建时刻（UTC）。</summary>
    /// <remarks>与 <see cref="TaskItem.CreatedAt"/> 同理，由创建方经 <c>IClock</c> 显式赋值。</remarks>
    public DateTime CreatedAt { get; set; }
}
