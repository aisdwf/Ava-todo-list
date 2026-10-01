using CommunityToolkit.Mvvm.ComponentModel;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 任务行的展示与就地编辑状态。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么在 <see cref="TaskItem"/> 之外再包一层</b>：任务行需要承载若干
/// **纯展示态**：标题是否处于就地编辑、编辑中的标题缓冲，
/// 以及为元信息行解析出的项目名。
/// </para>
/// <para>
/// 这些都不属于持久化领域数据。若塞进 <see cref="TaskItem"/>，
/// 「保存任务」就会连带写入一堆与数据库无关的 UI 状态（Article 10）。
/// </para>
/// <para>
/// <b>编辑缓冲与实体分离</b>：双击标题后输入落在 <see cref="TitleBuffer"/>，
/// 提交时才写回实体。Esc 取消时直接丢弃缓冲，实体从未被污染（spec-inline-task-edit）。
/// 所属项目创建后不可改（R-2.4 裁决），故行上不再持有项目编辑缓冲。
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

    /// <summary>标题是否处于双击后的就地编辑态。</summary>
    [ObservableProperty]
    private bool _isEditingTitle;

    /// <summary>标题编辑缓冲。与 <see cref="Title"/> 分离，Esc 时丢弃即可恢复原值。</summary>
    [ObservableProperty]
    private string _titleBuffer = string.Empty;

    /// <summary>所属项目名，用于元信息行。</summary>
    [ObservableProperty]
    private string _projectName = string.Empty;

    /// <summary>
    /// 标题的可绑定投影。实体不发通知，就地改名后经 <see cref="RefreshDerivedFlags"/> 刷新。
    /// </summary>
    public string Title => Task.Title;

    /// <summary>
    /// 优先级的可绑定投影，供行上的优先级标签、优先级色条与浮层展示。
    /// </summary>
    public Core.Enums.TaskPriority Priority => Task.Priority;

    /// <summary>是否归属某个项目，驱动元信息行项目名的显隐。</summary>
    public bool HasProject => !string.IsNullOrEmpty(Task.ProjectId);

    /// <summary>是否设有到期日，驱动到期徽标显隐。</summary>
    public bool HasDueDate => Task.DueDate is not null;

    /// <summary>
    /// 到期日的可绑定投影，供到期日选择器展示。实体不发通知，经 <see cref="RefreshDerivedFlags"/> 刷新。
    /// </summary>
    public DateTime? DueDate => Task.DueDate;

    /// <summary>
    /// 创建日期（本地时区）展示文案。
    /// </summary>
    /// <remarks>
    /// <c>CreatedAt</c> 是 UTC 时刻（<see cref="Core.Interfaces.IClock.UtcNow"/>）。此前视图直接
    /// <c>StringFormat MM/dd</c> 格式化 UTC 值，东八区 0–8 点创建的任务会显示成前一天。
    /// sqlite-net 读回的值 Kind 可能为 Unspecified，故显式按 UTC 解释后再转本地。
    /// </remarks>
    public string CreatedLocalText => FormatLocalDay(Task.CreatedAt);

    /// <summary>
    /// 已完成任务的完成日文案，如「09/29 已完成」。
    /// </summary>
    /// <remarks>
    /// 用户原话（2026-09-29）：「已完成的显示为 xx/xx已完成类似的形式感觉更合理，已经完成的任务显示逾期表现很怪」。
    /// 完成后到期日已失去行动意义，改为展示完成日；到期日本身仍保留在库中，取消勾选即恢复原展示。
    /// 历史数据可能缺 <c>CompletedAt</c>，此时只显示「已完成」。
    /// </remarks>
    public string CompletedLocalText =>
        Task.CompletedAt is { } at ? $"{FormatLocalDay(at)} 已完成" : "已完成";

    /// <summary>UTC 时刻按本地时区格式化为 MM/dd。sqlite-net 读回的 Kind 可能为 Unspecified，显式按 UTC 解释。</summary>
    private static string FormatLocalDay(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime()
            .ToString("MM/dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// 构造任务行。
    /// </summary>
    /// <param name="task">任务实体。</param>
    /// <param name="project">所属项目；查找失败时为 <c>null</c>（项目名为空）。</param>
    public TaskRowViewModel(TaskItem task, Project? project)
    {
        Task = task;
        IsCompleted = task.IsCompleted;
        ApplyProject(project);
    }

    /// <remarks>
    /// 勾选时 <c>CompletedAt</c> 由完成动作随后写入，这里先通知一次；
    /// 动作结束后的列表重载 / <see cref="ApplyPersisted"/> 会以落库值再刷新。
    /// </remarks>
    partial void OnIsCompletedChanged(bool value)
    {
        Task.IsCompleted = value;
        OnPropertyChanged(nameof(CompletedLocalText));
    }

    /// <summary>
    /// 用仓储读回的整行更新本行。归属项目变了则返回 false，由调用方整表重载以刷新项目名。
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
        OnPropertyChanged(nameof(CompletedLocalText));
    }

    /// <summary>
    /// 更新所属项目的展示信息。
    /// </summary>
    public void ApplyProject(Project? project)
    {
        ProjectName = project?.Name ?? string.Empty;
        OnPropertyChanged(nameof(HasProject));
    }

    /// <summary>
    /// 进入标题就地编辑，以当前标题预填缓冲。
    /// </summary>
    public void BeginTitleEdit()
    {
        TitleBuffer = Task.Title;
        IsEditingTitle = true;
    }

    /// <summary>
    /// 退出标题就地编辑并丢弃缓冲。提交与取消都以此收尾。
    /// </summary>
    public void EndTitleEdit()
    {
        IsEditingTitle = false;
        TitleBuffer = string.Empty;
    }

    /// <summary>
    /// 通知派生的投影属性重新求值。
    /// </summary>
    /// <remarks>
    /// 编辑提交后实体值已变，但 <c>Title</c> / <c>HasDueDate</c> 等是计算属性、
    /// 不会自动触发变更通知。Avalonia 对此不会报错 ——
    /// 界面只是静默地停留在旧状态（spec-editorial-and-ripple-theme 教训 1），故必须显式通知。
    /// </remarks>
    public void RefreshDerivedFlags()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(HasDueDate));
        OnPropertyChanged(nameof(DueDate));
        OnPropertyChanged(nameof(CompletedLocalText));
    }
}
