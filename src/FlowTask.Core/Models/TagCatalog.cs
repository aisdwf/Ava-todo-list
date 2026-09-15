using FlowTask.Core.Enums;

namespace FlowTask.Core.Models;

/// <summary>
/// 应用内置标签目录。
/// </summary>
/// <remarks>
/// 标签属于设置页可管理的预设集合。目录集中定义名称、颜色与顺序，
/// 避免启动注入逻辑与界面各自维护一份内置标签清单。
/// </remarks>
public static class TagCatalog
{
    /// <summary>
    /// 首次初始化时注入的内置标签。
    /// </summary>
    public static IReadOnlyList<TagDefinition> BuiltIns { get; } =
    [
        new("紧急", "#EF4444", 0),
        new("重要", "#F59E0B", 1),
        new("日常", "#10B981", 2),
        new("学习", "#3B82F6", 3),
        new("工作", "#8B5CF6", 4),
        new("个人", "#EC4899", 5)
    ];
}

/// <summary>
/// 内置标签的不可变定义。
/// </summary>
public sealed record TagDefinition(string Name, string ColorHex, int SortOrder);
