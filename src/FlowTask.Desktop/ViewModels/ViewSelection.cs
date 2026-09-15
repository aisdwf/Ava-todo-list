namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// <see cref="ViewSelection"/> 的维度取值。
/// </summary>
public enum ViewSelectionKind
{
    /// <summary>全部活跃任务。</summary>
    Active,

    /// <summary>已完成归档。</summary>
    Completed,

    /// <summary>某个具体项目。</summary>
    Project
}

/// <summary>
/// 侧边栏「当前查看什么」的单一状态量。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要这个类型</b>（spec-sidebar-selection-consolidation）：
/// 此前 VIEWS 筛选（<c>TaskFilter</c> 枚举）与项目筛选（<c>Project?</c>）
/// 是两个并行存在的状态，二者必须互斥却只能靠在对方的属性变更回调里
/// 手工清零来维持互斥。该结构已两次产出同类缺陷 ——
/// 「赋同值不触发变更回调」导致侧边栏高亮无法恢复
/// （详见 spec-editorial-and-ripple-theme、spec-classification-ui 的教训）。
/// </para>
/// <para>
/// 用一个 <c>record struct</c> 把「查看什么」表达为单一取值，
/// 使「同时只能选中一个」成为类型层面的保证：
/// <see cref="Kind"/> 为 <see cref="ViewSelectionKind.Project"/> 时
/// 天然排除了同时是 VIEWS 三项之一的可能，无需手工清零对侧状态。
/// </para>
/// <para>
/// 记录类型的默认结构相等性恰好是这里需要的语义：
/// 切换到不同的项目（<see cref="ProjectId"/> 不同）会被判定为「值变化」，
/// 从而正确触发 <c>ObservableProperty</c> 的变更回调 ——
/// 这正是根治此前「新值等于旧值时回调不触发」缺陷的关键。
/// </para>
/// </remarks>
public readonly record struct ViewSelection
{
    private ViewSelection(ViewSelectionKind kind, string? projectId)
    {
        Kind = kind;
        ProjectId = projectId;
    }

    /// <summary>当前选择的维度。</summary>
    public ViewSelectionKind Kind { get; }

    /// <summary>选中的项目 Id；仅 <see cref="Kind"/> 为 <see cref="ViewSelectionKind.Project"/> 时有值。</summary>
    public string? ProjectId { get; }

    /// <summary>全部活跃任务视图。</summary>
    public static ViewSelection Active { get; } = new(ViewSelectionKind.Active, null);

    /// <summary>已完成归档视图。</summary>
    public static ViewSelection Completed { get; } = new(ViewSelectionKind.Completed, null);

    /// <summary>构造指向某个项目的选择。</summary>
    /// <param name="projectId">目标项目 Id，不得为空。</param>
    public static ViewSelection ForProject(string projectId) => new(ViewSelectionKind.Project, projectId);
}
