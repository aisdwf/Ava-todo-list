namespace FlowTask.Core.Models;

/// <summary>
/// 系统种子项目 Default：承接「不想填项目」的零摩擦归属（R-2.6）。
/// </summary>
/// <remarks>
/// Id 固定，便于迁移与跨设备识别。名称可被展示层本地化，但 Id 不可变。
/// </remarks>
public static class DefaultProject
{
    /// <summary>稳定主键；勿改。</summary>
    public const string Id = "project-default";

    /// <summary>侧边栏与补全中的显示名。</summary>
    public const string Name = "Default";

    /// <summary>默认色（中性灰蓝）。</summary>
    public const string ColorHex = "#64748B";

    /// <summary>排序靠前，便于当作「不想填」入口。</summary>
    public const int SortOrder = 0;

    /// <summary>构造可持久化的种子实体（CreatedAt 由调用方经 IClock 赋值）。</summary>
    public static Project CreateSeed(DateTime createdAtUtc) => new()
    {
        Id = Id,
        Name = Name,
        ColorHex = ColorHex,
        SortOrder = SortOrder,
        IsArchived = false,
        CreatedAt = createdAtUtc
    };
}
