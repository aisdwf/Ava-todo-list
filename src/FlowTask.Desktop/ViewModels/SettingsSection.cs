namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 设置页左侧导航的具体设置项（spec-settings-master-detail-and-theme-presets）。
/// </summary>
/// <remarks>
/// 用普通枚举而非类似 <see cref="ViewSelection"/> 的 record struct：
/// 设置项是固定的静态集合，不像项目筛选那样需要携带一个动态标识
/// （如 <c>ProjectId</c>），枚举已足够表达"当前选中哪一项"且类型更轻。
/// </remarks>
public enum SettingsSection
{
    /// <summary>外观主题（命名主题预设）。</summary>
    ThemePreset,

    /// <summary>强调色。</summary>
    Accent,

    /// <summary>窗口材质。</summary>
    Material,

    /// <summary>默认到期偏移。</summary>
    DueDateOffset,

    /// <summary>关于。</summary>
    About
}

/// <summary>
/// 设置页左侧导航列表的一个条目。
/// </summary>
/// <param name="Section">该条目对应的设置项。</param>
/// <param name="DisplayName">左侧列表展示的名称。</param>
/// <param name="Description">列表项下方的一句话说明。</param>
public sealed record SettingsNavItem(SettingsSection Section, string DisplayName, string Description);
