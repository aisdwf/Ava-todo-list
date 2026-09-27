using CommunityToolkit.Mvvm.ComponentModel;
using FlowTask.Core.Enums;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 编辑态中「所属项目」下拉的候选项。
/// </summary>
/// <remarks>
/// ComboBox 需要对象实例作为条目。R-2.6 以 Default 取代「未归属」，
/// 故候选项的 <see cref="ProjectId"/> 必须是真实项目，不再用 <c>null</c> 表示空归属。
/// </remarks>
public sealed class ProjectChoice
{
    /// <summary>项目 Id；必为已存在项目（含 Default 与当前任务所属的归档项目）。</summary>
    public string ProjectId { get; }

    /// <summary>下拉中显示的名称。</summary>
    public string DisplayName { get; }

    public ProjectChoice(string projectId, string displayName)
    {
        ProjectId = projectId;
        DisplayName = displayName;
    }

    /// <summary>Default 候选项，与 <see cref="DefaultProject"/> 同源（Article 6）。</summary>
    public static ProjectChoice Default { get; } = new(DefaultProject.Id, DefaultProject.Name);
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

    /// <summary>
    /// 完成态的可绑定投影。实体本身不发通知，勾选圈必须绑这里。
    /// </summary>
    [ObservableProperty]
    private bool _isCompleted;

    /// <summary>是否展开编辑面板。</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>编辑缓冲：标题。</summary>
    [ObservableProperty]
    private string _editTitle = string.Empty;

    /// <summary>编辑缓冲：优先级。</summary>
    [ObservableProperty]
    private TaskPriority _editPriority;

    /// <summary>编辑缓冲：所属项目。</summary>
    [ObservableProperty]
    private ProjectChoice _editProject = ProjectChoice.Default;

    /// <summary>所属项目名，用于色条的悬浮提示。</summary>
    [ObservableProperty]
    private string _projectName = string.Empty;

    /// <summary>所属项目色值，用于渲染色条。</summary>
    [ObservableProperty]
    private string _projectColorHex = string.Empty;

    /// <summary>是否归属某个项目，驱动色条显隐。</summary>
    public bool HasProject => !string.IsNullOrEmpty(Task.ProjectId);

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
    /// <param name="project">所属项目；查找失败时为 <c>null</c>（色条不画，编辑时回落到 Default）。</param>
    public TaskRowViewModel(TaskItem task, Project? project)
    {
        Task = task;
        IsCompleted = task.IsCompleted;
        ApplyProject(project);
    }

    partial void OnIsCompletedChanged(bool value) => Task.IsCompleted = value;

    /// <summary>
    /// 用仓储读回的整行更新本行。归属项目变了则返回 false，由调用方整表重载以刷新色条。
    /// </summary>
    public void ApplyPersisted(TaskItem persisted)
    {
        Task.Title = persisted.Title;
        Task.Priority = persisted.Priority;
        Task.DueDate = persisted.DueDate;
        Task.CompletedAt = persisted.CompletedAt;
        IsCompleted = persisted.IsCompleted;
        RefreshDerivedFlags();
    }

    /// <summary>
    /// 用仓储读回的完成态更新本行。保留同一行实例，供已渲染的勾选圈接收通知。
    /// </summary>
    public void ApplyPersistedCompletion(bool isCompleted, DateTime? completedAt)
    {
        Task.CompletedAt = completedAt;
        IsCompleted = isCompleted;
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
    /// <remarks>
    /// 侧边栏候选只含未归档项目。全部任务看板仍会列出归档项目下的任务；
    /// 找不到当前归属时补进候选（同一实例，供 ComboBox 引用相等）。
    /// 历史 <c>ProjectId == null</c> 的行回落到 Default，不再提供「未归属」。
    /// </remarks>
    public void BeginEdit(IList<ProjectChoice> projectChoices)
    {
        EditTitle = Task.Title;
        EditPriority = Task.Priority;

        var match = projectChoices.FirstOrDefault(c => c.ProjectId == Task.ProjectId);
        if (match is null && Task.ProjectId is not null)
        {
            var label = string.IsNullOrEmpty(ProjectName) ? Task.ProjectId : ProjectName;
            match = new ProjectChoice(Task.ProjectId, label);
            projectChoices.Add(match);
        }

        EditProject = match
                      ?? projectChoices.FirstOrDefault(c => c.ProjectId == DefaultProject.Id)
                      ?? ProjectChoice.Default;
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
    /// 编辑提交后实体值已变，但 <c>HasDueDate</c> 是计算属性、
    /// 不会自动触发变更通知。Avalonia 对此不会报错 ——
    /// 界面只是静默地停留在旧状态（spec-editorial-and-ripple-theme 教训 1），故必须显式通知。
    /// </remarks>
    public void RefreshDerivedFlags()
    {
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(HasDueDate));
    }
}
