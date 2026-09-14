using CommunityToolkit.Mvvm.ComponentModel;
using FlowTask.Core.Enums;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 编辑态中「所属项目」下拉的候选项。
/// </summary>
/// <remarks>
/// 需要一个表示「未归属」的候选项，而 <c>null</c> 无法作为 ComboBox 的可选条目
/// 被正常显示与选中。此包装使「未归属」成为一个显式、可选择的一等选项 ——
/// 这与零必填原则一致：未归属不是缺失状态，而是正常的默认状态。
/// </remarks>
public sealed class ProjectChoice
{
    /// <summary>项目 Id；<c>null</c> 表示未归属。</summary>
    public string? ProjectId { get; }

    /// <summary>下拉中显示的名称。</summary>
    public string DisplayName { get; }

    public ProjectChoice(string? projectId, string displayName)
    {
        ProjectId = projectId;
        DisplayName = displayName;
    }

    /// <summary>「未归属」候选项。</summary>
    public static ProjectChoice None { get; } = new(null, "未归属");
}

/// <summary>
/// 任务行的展示与编辑状态。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么在 <see cref="TaskItem"/> 之外再包一层</b>：任务行需要承载若干
/// **纯展示态**：是否展开编辑面板、编辑中的各字段缓冲值、
/// 以及为渲染色条而解析出的项目名与项目色。
/// </para>
/// <para>
/// 这些都不属于持久化领域数据。若塞进 <see cref="TaskItem"/>，
/// 「保存任务」就会连带写入一堆与数据库无关的 UI 状态（Article 10）。
/// 且 <see cref="TaskItem"/> 是 Core 层实体，不应知道「编辑面板」这种 UI 概念。
/// </para>
/// <para>
/// <b>编辑缓冲与实体分离</b>：编辑中的值先落在 <see cref="EditTitle"/> 等缓冲属性，
/// 提交时才写回实体。这样非法输入（空标题、错误日期格式）可以被拒绝并恢复原值，
/// 而不会先污染实体再试图回滚。
/// </para>
/// </remarks>
public partial class TaskRowViewModel : ViewModelBase
{
    /// <summary>被包装的任务实体。</summary>
    public TaskItem Task { get; }

    /// <summary>是否展开编辑面板。</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>编辑缓冲：标题。</summary>
    [ObservableProperty]
    private string _editTitle = string.Empty;

    /// <summary>编辑缓冲：标签（逗号分隔的原始文本）。</summary>
    [ObservableProperty]
    private string _editTags = string.Empty;

    /// <summary>编辑缓冲：到期日文本（`yyyy-MM-dd`，空串表示未安排）。</summary>
    [ObservableProperty]
    private string _editDueDate = string.Empty;

    /// <summary>编辑缓冲：优先级。</summary>
    [ObservableProperty]
    private TaskPriority _editPriority;

    /// <summary>编辑缓冲：所属项目。</summary>
    [ObservableProperty]
    private ProjectChoice _editProject = ProjectChoice.None;

    /// <summary>所属项目名，用于色条的悬浮提示。</summary>
    [ObservableProperty]
    private string _projectName = string.Empty;

    /// <summary>所属项目色值，用于渲染色条。</summary>
    [ObservableProperty]
    private string _projectColorHex = string.Empty;

    /// <summary>是否归属某个项目，驱动色条显隐。</summary>
    public bool HasProject => !string.IsNullOrEmpty(Task.ProjectId);

    /// <summary>是否含标签，驱动标签区显隐。</summary>
    public bool HasTags => !string.IsNullOrWhiteSpace(Task.Tags);

    /// <summary>是否设有到期日，驱动到期徽标显隐。</summary>
    public bool HasDueDate => Task.DueDate is not null;

    /// <summary>
    /// 优先级单选组名。
    /// </summary>
    /// <remarks>
    /// 每行必须持有**唯一**的组名：<c>RadioButton</c> 的 <c>GroupName</c>
    /// 在同一可视树内共享作用域，若所有行用同一组名，
    /// 选中任一行的优先级会取消其他所有行的选中 —— 表现为多行编辑时优先级互相干扰。
    /// 以任务 Id 派生保证唯一。
    /// </remarks>
    public string GroupName => $"EditPriority_{Task.Id}";

    /// <summary>
    /// 构造任务行。
    /// </summary>
    /// <param name="task">任务实体。</param>
    /// <param name="project">所属项目；<c>null</c> 表示未归属。</param>
    public TaskRowViewModel(TaskItem task, Project? project)
    {
        Task = task;
        ApplyProject(project);
    }

    /// <summary>
    /// 更新所属项目的展示信息。
    /// </summary>
    public void ApplyProject(Project? project)
    {
        ProjectName = project?.Name ?? string.Empty;
        ProjectColorHex = project?.ColorHex ?? string.Empty;
        OnPropertyChanged(nameof(HasProject));
    }

    /// <summary>
    /// 以实体当前值填充编辑缓冲并展开面板。
    /// </summary>
    /// <param name="projectChoices">可选项目列表，用于定位当前归属对应的候选项。</param>
    public void BeginEdit(IEnumerable<ProjectChoice> projectChoices)
    {
        EditTitle = Task.Title;
        EditTags = Task.Tags ?? string.Empty;
        EditDueDate = Task.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        EditPriority = Task.Priority;
        EditProject = projectChoices.FirstOrDefault(c => c.ProjectId == Task.ProjectId)
                      ?? ProjectChoice.None;

        IsEditing = true;
    }

    /// <summary>
    /// 收起编辑面板。
    /// </summary>
    public void EndEdit() => IsEditing = false;

    /// <summary>
    /// 通知派生的显隐标志重新求值。
    /// </summary>
    /// <remarks>
    /// 编辑提交后实体值已变，但 <c>HasTags</c> / <c>HasDueDate</c> 是计算属性、
    /// 不会自动触发变更通知。Avalonia 对此不会报错 ——
    /// 界面只是静默地停留在旧状态（SPEC-0003 教训 1），故必须显式通知。
    /// </remarks>
    public void RefreshDerivedFlags()
    {
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(HasTags));
        OnPropertyChanged(nameof(HasDueDate));
    }
}
