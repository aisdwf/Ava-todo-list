using CommunityToolkit.Mvvm.ComponentModel;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 侧边栏单个项目行的展示状态。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要这层包装</b>：侧边栏每行要显示项目名、项目色与「该项目下未完成任务数」。
/// 前两者是 <see cref="Project"/> 的持久化属性，而计数是**派生的展示态** ——
/// 它随任务增删实时变化，不属于项目自身的领域数据。
/// </para>
/// <para>
/// 若把计数塞进 <see cref="Project"/> 实体，「保存项目」就会连带写入一个
/// 可能已经过期的冗余值，且该值与 Tasks 表的真实状态存在两份表示（Article 6）。
/// 包装成 ViewModel 后，实体保持纯粹，计数只活在展示层。
/// </para>
/// <para>
/// 同时承载重命名的编辑态 —— 双击项目名进入原地编辑，
/// 该状态是纯 UI 概念，同样不应污染实体。
/// </para>
/// </remarks>
public partial class ProjectItemViewModel : ViewModelBase
{
    /// <summary>被包装的项目实体。</summary>
    public Project Project { get; }

    /// <summary>项目 Id，便于绑定与比较。</summary>
    public string Id => Project.Id;

    /// <summary>项目名。</summary>
    /// <remarks>
    /// 经本属性而非直接读 <c>Project.Name</c>：重命名后需通知界面刷新，
    /// 而 <see cref="Project"/> 是纯数据类、不实现变更通知。
    /// </remarks>
    [ObservableProperty]
    private string _name;

    /// <summary>项目色（`#RRGGBB`）。</summary>
    [ObservableProperty]
    private string _colorHex;

    /// <summary>该项目下未完成任务数。</summary>
    [ObservableProperty]
    private int _taskCount;

    /// <summary>是否为当前选中的项目，驱动侧边栏高亮。</summary>
    /// <remarks>
    /// 由 <c>MainViewModel</c> 在切换选中项时统一维护。
    /// 以每行自持标志而非「行与全局 SelectedProject 比对」的形式表达：
    /// 后者需要在 XAML 里做跨作用域绑定与相等转换，
    /// 而 Avalonia 对未匹配的绑定路径静默失败 —— 高亮失效不会有任何报错
    /// （spec-editorial-and-ripple-theme 教训 1）。自持布尔量是可直接绑定、可被测试断言的形式。
    /// </remarks>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>是否处于原地重命名编辑态。</summary>
    [ObservableProperty]
    private bool _isRenaming;

    /// <summary>重命名输入缓冲。</summary>
    /// <remarks>
    /// 与 <see cref="Name"/> 分离：用户取消重命名（Esc）时需丢弃输入并保留原名，
    /// 若直接双向绑定 <see cref="Name"/> 则原值已被覆盖、无从恢复。
    /// </remarks>
    [ObservableProperty]
    private string _renameBuffer = string.Empty;

    /// <summary>
    /// 构造项目行。
    /// </summary>
    /// <param name="project">项目实体。</param>
    /// <param name="taskCount">该项目下未完成任务数。</param>
    public ProjectItemViewModel(Project project, int taskCount)
    {
        Project = project;
        _name = project.Name;
        _colorHex = project.ColorHex;
        _taskCount = taskCount;
    }

    /// <summary>
    /// 进入重命名编辑态，以当前名称预填缓冲。
    /// </summary>
    public void BeginRename()
    {
        RenameBuffer = Name;
        IsRenaming = true;
    }

    /// <summary>
    /// 放弃重命名，丢弃缓冲内容。
    /// </summary>
    public void CancelRename()
    {
        IsRenaming = false;
        RenameBuffer = string.Empty;
    }

    /// <summary>
    /// 将实体的最新值同步到展示属性。
    /// </summary>
    /// <remarks>
    /// 用于重命名或改色落库后刷新界面，避免整体重建集合导致侧边栏选中态丢失。
    /// </remarks>
    public void SyncFromEntity()
    {
        Name = Project.Name;
        ColorHex = Project.ColorHex;
    }
}
