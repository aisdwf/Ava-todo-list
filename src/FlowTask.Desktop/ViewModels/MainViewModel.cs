using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FlowTask.Core.Enums;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Messages;
using FlowTask.Core.Models;
using FlowTask.Desktop.Appearance;
using FlowTask.Desktop.ViewModels.Actions;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 任务视图筛选维度。
/// </summary>
/// <remarks>
/// 为什么独立成枚举：原实现以裸字符串 "All" / "Today" / "Completed" / "Settings" 表示筛选，
/// 把"设置页"混入任务筛选枚举，导致视图模式与数据筛选两个正交概念被耦合在同一状态里
/// （Article 10）。此处只保留真正的任务筛选维度，设置页由 <see cref="MainViewModel.IsSettingsOpen"/>
/// 独立表达。
/// </remarks>
public enum TaskFilter
{
    /// <summary>全部活跃任务。</summary>
    Active,

    /// <summary>已完成归档。</summary>
    Completed,

    /// <summary>设置。</summary>
    Settings
}

/// <summary>
/// 主工作台视图模型：承载 Editorial 任务流、视图筛选与外观个性化状态。
/// </summary>
public partial class MainViewModel : ViewModelBase, IRecipient<TaskSavedMessage>, IRecipient<TaskDeletedMessage>
{
    private readonly ITaskRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly IClock _clock;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    /// <summary>
    /// 设置视图是否展开。与任务筛选正交，因此不占用 <see cref="ViewSelection"/> 的取值位。
    /// </summary>
    [ObservableProperty]
    private bool _isSettingsOpen;

    /// <summary>
    /// 侧边栏「当前查看什么」的单一状态量。
    /// </summary>
    /// <remarks>
    /// <b>本 SPEC 的核心整改</b>（spec-sidebar-selection-consolidation）：
    /// VIEWS 三项与项目筛选此前是两套并行状态，互斥全靠手工清零维持，
    /// 已两次产出「赋同值不触发变更回调、高亮无法恢复」的同类缺陷。
    /// 收敛为单一状态量后，「同时只能选中一个」由类型保证 ——
    /// 赋一个新的 <see cref="ViewSelection"/>（哪怕 <c>Kind</c> 相同、<c>ProjectId</c> 不同）
    /// 都会被记录类型的相等性判定为「值变化」，从而正确触发下方的派生属性同步。
    /// <para>
    /// <see cref="CurrentFilter"/>、<see cref="SelectedProject"/> 等公开属性保留为
    /// 只读派生视图，供既有绑定与测试断言继续读取，避免一次性改动过大。
    /// </para>
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFilter))]
    [NotifyPropertyChangedFor(nameof(SelectedProject))]
    [NotifyPropertyChangedFor(nameof(IsActiveFilterSelected))]
    [NotifyPropertyChangedFor(nameof(IsCompletedFilterSelected))]
    private ViewSelection _currentSelection = ViewSelection.Active;

    /// <summary>
    /// 当前窗口材质预设。以对象而非 Id 字符串持有，直接充当 ListBox 的 SelectedItem，
    /// 省去一层 Id 到对象的查找映射。
    /// </summary>
    [ObservableProperty]
    private MaterialOption _selectedMaterial = AppearanceCoordinator.MaterialPresets[0];

    /// <summary>当前强调色预设。</summary>
    [ObservableProperty]
    private AppearanceOption _selectedAccent = AppearanceCoordinator.AccentPresets[0];

    /// <summary>
    /// 当前 VIEWS 筛选维度，由 <see cref="CurrentSelection"/> 派生。
    /// </summary>
    /// <remarks>
    /// 项目筛选生效时（<c>CurrentSelection.Kind == Project</c>）此属性回落为
    /// <see cref="TaskFilter.Active"/> —— 该取值此时不驱动任何 UI 高亮
    /// （侧边栏高亮统一经 <see cref="ViewSelection"/> 直接比对，见 §2.2），
    /// 仅在 <see cref="LoadTasksAsync"/> 判断项目筛选优先级时作为占位默认值。
    /// </remarks>
    public TaskFilter CurrentFilter => CurrentSelection.Kind switch
    {
        ViewSelectionKind.Completed => TaskFilter.Completed,
        _ => TaskFilter.Active
    };

    /// <summary>
    /// 当前选中的项目，由 <see cref="CurrentSelection"/> 派生；<c>null</c> 表示未启用项目筛选。
    /// </summary>
    public Project? SelectedProject => CurrentSelection.Kind == ViewSelectionKind.Project
        ? _projects.FirstOrDefault(p => p.Id == CurrentSelection.ProjectId)?.Project
        : null;

    /// <summary>VIEWS「全部任务」是否高亮，由 <see cref="CurrentSelection"/> 派生。</summary>
    public bool IsActiveFilterSelected => CurrentSelection.Kind == ViewSelectionKind.Active;

    /// <summary>VIEWS「已完成归档」是否高亮，由 <see cref="CurrentSelection"/> 派生。</summary>
    public bool IsCompletedFilterSelected => CurrentSelection.Kind == ViewSelectionKind.Completed;

    [ObservableProperty]
    private string _currentCategoryTitle = "全部任务";

    [ObservableProperty]
    private string _currentCategorySubtitle = "聚焦所有活跃进行中的待办";

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    /// <summary>新任务的优先级档位，默认中优先级。</summary>
    [ObservableProperty]
    private TaskPriority _newTaskPriority = TaskPriority.Medium;

    /// <summary>默认到期偏移天数（来自设置）。</summary>
    [ObservableProperty]
    private int _defaultDueOffsetDays = DueDateOffset.DefaultDays;

    /// <summary>创建区中的到期日编辑器。</summary>
    public DueDateEditorViewModel NewDueDateEditor { get; private set; } = null!;

    /// <summary>行编辑弹出层中的到期日编辑器。</summary>
    public DueDateEditorViewModel EditingDueDateEditor { get; private set; } = null!;

    /// <summary>是否打开到期日编辑弹出层。</summary>
    [ObservableProperty]
    private bool _isDueDatePopupOpen;

    /// <summary>正在编辑的任务行（用于弹出层定位）；<c>null</c> 表示未在弹出编辑。</summary>
    [ObservableProperty]
    private TaskRowViewModel? _editingDueDateTarget;

    [ObservableProperty]
    private int _activeCount;

    /// <summary>「已完成归档」计数，语义为已归档任务数（spec-task-complete-before-archive）。</summary>
    [ObservableProperty]
    private int _completedCount;

    /// <summary>
    /// 已完成但尚未归档的任务数，驱动「归档全部已完成」按钮的可用/可见状态。
    /// </summary>
    /// <remarks>0 时该按钮应禁用或隐藏，避免空操作（spec-task-complete-before-archive §2.4 D3）。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingArchive))]
    private int _pendingArchiveCount;

    /// <summary>是否存在可归档的已完成任务，驱动按钮 IsVisible/IsEnabled 绑定。</summary>
    public bool HasPendingArchive => PendingArchiveCount > 0;

    /// <summary>
    /// 当前筛选下的任务行集合（只读投影）。
    /// </summary>
    /// <remarks>
    /// <b>为什么暴露为只读</b>：任务的唯一写入路径是仓储。
    /// 若外部可直接 <c>Add</c> / <c>Remove</c> 本集合，
    /// 界面就会显示未落库的幽灵数据，且该违规无法被编译器发现。
    /// 以类型强制此约束，而非仅在注释里约定（借鉴 xin-07/TodoList 的做法）。
    /// 集合内容只能经 <see cref="LoadTasksAsync"/> 从仓储重建。
    /// <para>
    /// 元素为 <see cref="TaskRowViewModel"/> 而非裸实体：行需要承载编辑态与
    /// 项目色等纯展示信息，这些不应污染 Core 层实体（Article 10）。
    /// </para>
    /// </remarks>
    public ReadOnlyObservableCollection<TaskRowViewModel> Tasks { get; }

    /// <summary>可变的内部集合，仅本类可写。</summary>
    private readonly ObservableCollection<TaskRowViewModel> _tasks = new();

    /// <summary>
    /// 编辑态「所属项目」下拉的候选项，首项恒为「未归属」。
    /// </summary>
    /// <remarks>
    /// 「未归属」作为一等候选项而非以 <c>null</c> 表达：
    /// 依零必填原则它是正常的默认状态，用户须能主动选回该状态。
    /// </remarks>
    public ObservableCollection<ProjectChoice> ProjectChoices { get; } = new() { ProjectChoice.None };

    /// <summary>任务流为空，用于驱动空状态提示。</summary>
    public bool IsTaskStreamEmpty => Tasks.Count == 0;

    /// <summary>侧边栏项目行列表（只读投影，与 <see cref="Tasks"/> 同理）。</summary>
    /// <remarks>
    /// 元素为 <see cref="ProjectItemViewModel"/> 而非裸 <see cref="Project"/>：
    /// 侧边栏每行需同时展示项目属性与其任务计数，
    /// 而计数是派生的展示态、不属于持久化领域数据。
    /// 若把计数加到 <see cref="Project"/> 实体上，「保存项目」会意外携带一个可能过期的冗余值。
    /// </remarks>
    public ReadOnlyObservableCollection<ProjectItemViewModel> Projects { get; }

    /// <summary>可变的内部项目集合，仅本类可写。</summary>
    private readonly ObservableCollection<ProjectItemViewModel> _projects = new();

    /// <summary>设置页中的标签管理列表。</summary>
    public ReadOnlyObservableCollection<TagItemViewModel> Tags { get; }

    /// <summary>可变的内部标签集合，仅本类可写。</summary>
    private readonly ObservableCollection<TagItemViewModel> _tags = new();

    /// <summary>创建区中的标签选择项。</summary>
    public ObservableCollection<TagChoice> NewTagChoices { get; } = new();

    /// <summary>新建标签的名称输入。</summary>
    [ObservableProperty]
    private string _newTagName = string.Empty;

    /// <summary>
    /// 是否存在任何项目。
    /// </summary>
    /// <remarks>
    /// 驱动侧边栏 PROJECTS 区块的整块显隐。零项目时整块隐藏 ——
    /// 不显示空列表，也不显示「新建项目」占位，
    /// 使从不使用项目的用户获得与改动前完全一致的体验（design-domain-contract §3.3）。
    /// </remarks>
    public bool HasProjects => _projects.Count > 0;

    /// <summary>可选强调色预设，直接引用权威定义避免影子副本 (Article 6)。</summary>
    public IReadOnlyList<AppearanceOption> AccentPresets => AppearanceCoordinator.AccentPresets;

    /// <summary>可选窗口材质预设，直接引用权威定义。</summary>
    public IReadOnlyList<MaterialOption> MaterialPresets => AppearanceCoordinator.MaterialPresets;

    /// <summary>请求唤起随手记浮窗。由视图层订阅，ViewModel 不持有窗口引用。</summary>
    public event Action? RequestOpenQuickCapture;

    /// <summary>
    /// 唤起小窗热键的平台正确按键提示（macOS 显示 ⌥ 符号，Windows 显示 Alt 文字）。
    /// </summary>
    /// <remarks>
    /// 此前 UI 写死 macOS 的 <c>⌥ Space</c>，Windows 用户看到的图标与实际热键不符。
    /// </remarks>
    public string QuickCaptureHotkeyLabel => OperatingSystem.IsMacOS() ? "⌥ Space" : "Alt+Space";

    /// <summary>请求应用窗口材质。窗口实例归视图层所有，故以事件外发。</summary>
    public event Action<MaterialOption>? MaterialPresetChanged;

    /// <summary>主题变体已应用，通知视图层重建依赖具体笔刷实例的视觉属性。</summary>
    public event Action? ThemeApplied;

    /// <summary>
    /// 构造主视图模型。
    /// </summary>
    /// <param name="repository">任务仓储。</param>
    /// <param name="projectRepository">项目仓储。</param>
    /// <param name="tagRepository">标签仓储。</param>
    /// <param name="clock">时间提供者，用于显式赋值任务的创建与完成时刻（Article 9）。</param>
    /// <param name="settingsRepository">应用设置仓储。</param>
    public MainViewModel(
        ITaskRepository repository,
        IProjectRepository projectRepository,
        ITagRepository tagRepository,
        IClock clock,
        IAppSettingsRepository settingsRepository)
    {
        _repository = repository;
        _projectRepository = projectRepository;
        _tagRepository = tagRepository;
        _clock = clock;
        _settingsRepository = settingsRepository;

        // 初始化到期日编辑器（Func<int> 绑定 DefaultDueOffsetDays 属性）
        NewDueDateEditor = new DueDateEditorViewModel(clock, () => DefaultDueOffsetDays);
        EditingDueDateEditor = new DueDateEditorViewModel(clock, () => DefaultDueOffsetDays);

        Tasks = new ReadOnlyObservableCollection<TaskRowViewModel>(_tasks);
        Projects = new ReadOnlyObservableCollection<ProjectItemViewModel>(_projects);
        Tags = new ReadOnlyObservableCollection<TagItemViewModel>(_tags);

        _projects.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasProjects));

        // 弱引用消息总线：随手记浮窗写入后通知主窗口刷新，双方互不持有强引用 (rule-code-standards §2.1)
        WeakReferenceMessenger.Default.Register<TaskSavedMessage>(this);
        WeakReferenceMessenger.Default.Register<TaskDeletedMessage>(this);

        _tasks.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsTaskStreamEmpty));
    }

    /// <summary>
    /// 首次载入任务与统计数据。
    /// </summary>
    public async Task InitializeAsync()
    {
        await _settingsRepository.InitializeAsync();
        DefaultDueOffsetDays = await _settingsRepository.GetDefaultDueOffsetDaysAsync();

        // R-2.6：启动时确保 Default 项目存在，并将历史 ProjectId=null 迁过去
        await _projectRepository.EnsureDefaultProjectAsync(_clock.UtcNow);

        AppearanceCoordinator.ApplyAccent(SelectedAccent.Id);
        await LoadTagsAsync();
        await LoadProjectsAsync();
        await LoadTasksAsync();
    }

    /// <summary>
    /// 载入标签管理列表，并将使用计数绑定到设置页。
    /// </summary>
    private async Task LoadTagsAsync()
    {
        var tags = await _tagRepository.GetAllAsync();
        var usageCounts = await _tagRepository.GetUsageCountsAsync();

        _tags.Clear();
        var selectedNewTagIds = NewTagChoices
            .Where(choice => choice.IsSelected)
            .Select(choice => choice.Tag.Id)
            .ToHashSet(StringComparer.Ordinal);
        NewTagChoices.Clear();
        foreach (var tag in tags)
        {
            usageCounts.TryGetValue(tag.Id, out var count);
            _tags.Add(new TagItemViewModel(tag, count));
            NewTagChoices.Add(new TagChoice(tag, selectedNewTagIds.Contains(tag.Id)));
        }
    }

    /// <summary>
    /// 载入未归档项目及其任务计数，并同步编辑态下拉候选。
    /// </summary>
    private async Task LoadProjectsAsync()
    {
        var projects = await _projectRepository.GetActiveProjectsAsync();

        // 在重建 _projects 之前先记录选中项目 Id：CurrentSelection.ProjectId 独立于
        // _projects 集合存在，但 SelectedProject 派生属性依赖 _projects 查找，
        // 集合被 Clear() 后该属性会短暂返回 null，须先取原始 Id 才能正确判断存续。
        var selectedProjectId = CurrentSelection.Kind == ViewSelectionKind.Project
            ? CurrentSelection.ProjectId
            : null;

        _projects.Clear();
        foreach (var project in projects)
        {
            var count = await _projectRepository.CountTasksAsync(project.Id);
            _projects.Add(new ProjectItemViewModel(project, count));
        }

        // 候选项与项目列表保持同源，避免两份表示漂移（Article 6）
        ProjectChoices.Clear();
        ProjectChoices.Add(ProjectChoice.None);
        foreach (var project in projects)
        {
            ProjectChoices.Add(new ProjectChoice(project.Id, project.Name));
        }

        // 选中的项目可能已被删除或归档，此时须解除选中避免筛选到不存在的项目。
        // 经 ReturnToActiveViewAsync 显式重建状态 —— 赋一个新的 ViewSelection
        // 即可保证被记录类型的相等性判定为「值变化」，无需再担心回调不触发
        if (selectedProjectId is not null
            && _projects.All(p => p.Id != selectedProjectId))
        {
            await ReturnToActiveViewAsync();
            return;
        }

        // 集合已重建为新实例，须重新同步高亮标志到新实例上，否则高亮丢失
        SyncProjectSelectionFlags();
    }

    /// <summary>
    /// 统一选中状态变更时同步派生展示：标题文案与侧边栏项目行高亮。
    /// </summary>
    /// <remarks>
    /// <b>为什么不在此触发 <see cref="LoadTasksAsync"/></b>：本回调只在
    /// <see cref="CurrentSelection"/> 的值**真正变化**时触发（记录类型的相等性保证），
    /// 而各写入路径对"是否需要重新加载任务"的要求并不相同 ——
    /// <see cref="ChangeFilter"/> 需要在值不变时仍关闭设置页但跳过重载，
    /// <see cref="SelectProjectAsync"/> 与 <see cref="ReturnToActiveViewAsync"/>
    /// 需要无条件同步等待加载完成（供调用方在 await 后立即读取 <see cref="Tasks"/>）。
    /// 两种取舍无法用同一个属性变更回调满足，因此加载动作留在各写入路径自行决定，
    /// 本回调只负责与"值确实变了"强绑定的展示同步。
    /// </remarks>
    partial void OnCurrentSelectionChanged(ViewSelection value)
    {
        (CurrentCategoryTitle, CurrentCategorySubtitle) = value.Kind switch
        {
            ViewSelectionKind.Completed => ("已完成归档", "所有已达成的历史成果记录"),
            ViewSelectionKind.Project => (ResolveProjectName(value.ProjectId), "该项目下进行中的待办"),
            _ => ("全部任务", "聚焦所有活跃进行中的待办")
        };

        SyncProjectSelectionFlags();
    }

    /// <summary>按 Id 解析项目名，用于选中项目时的标题展示。</summary>
    private string ResolveProjectName(string? projectId) =>
        _projects.FirstOrDefault(p => p.Id == projectId)?.Name ?? string.Empty;

    /// <summary>
    /// 选中项目并切换到该项目的任务列表。
    /// </summary>
    /// <param name="project">目标项目行；传 <c>null</c> 回到「全部任务」。</param>
    [RelayCommand]
    private async Task SelectProjectAsync(ProjectItemViewModel? project)
        => await new SelectProjectViewModel().ExecuteAsync(
            project,
            () => IsSettingsOpen = false,
            ReturnToActiveViewAsync,
            id => CurrentSelection = ViewSelection.ForProject(id),
            LoadTasksAsync);

    /// <summary>
    /// 解除项目筛选，回到「全部任务」视图。
    /// </summary>
    /// <remarks>
    /// 只需赋一次统一状态，互斥与高亮的重建交由
    /// <see cref="OnCurrentSelectionChanged"/> 统一处理 ——
    /// 不再需要像旧实现那样手工清零三个 <c>IsXxxFilterSelected</c> 布尔量
    /// （该手工同步正是 spec-editorial-and-ripple-theme / spec-classification-ui
    /// 两次同类缺陷的根源，见 SPEC §1.2）。
    /// </remarks>
    private async Task ReturnToActiveViewAsync()
    {
        CurrentSelection = ViewSelection.Active;
        await LoadTasksAsync();
    }

    /// <summary>
    /// 切换任务筛选维度。
    /// </summary>
    /// <remarks>
    /// 关闭设置页无条件执行；但仅在目标筛选与当前值**确实不同**时才触发重新加载 ——
    /// 用户点击当前已选中的导航项不应引发多余的数据库查询。
    /// 该判断显式进行（而非依赖属性变更回调是否触发），
    /// 因为回调本身不适合承载"是否加载"的决策（见 <see cref="OnCurrentSelectionChanged"/> 注释）。
    /// </remarks>
    [RelayCommand]
    private void ChangeFilter(TaskFilter filter)
        => new ChangeFilterViewModel().Execute(
            filter,
            CurrentSelection,
            () => IsSettingsOpen = false,
            selection => CurrentSelection = selection,
            () => _ = LoadTasksAsync());

    /// <summary>
    /// 按当前筛选载入任务，并同步侧边栏计数。
    /// </summary>
    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        // 项目筛选优先于 VIEWS 筛选：二者现由同一状态量表达为互斥的不同取值
        var items = CurrentSelection.Kind == ViewSelectionKind.Project
            ? await _repository.GetTasksByProjectAsync(CurrentSelection.ProjectId!)
            : CurrentFilter switch
            {
                TaskFilter.Completed => await _repository.GetCompletedTasksAsync(),
                _ => await _repository.GetAllActiveTasksAsync()
            };

        // 建立 Id → 项目 的查找表，避免为每行任务各查一次库（N+1 查询）
        var projectLookup = _projects.ToDictionary(p => p.Id, p => p.Project);
        var tagLookup = await _tagRepository.GetTagsForTasksAsync(items.Select(item => item.Id));

        _tasks.Clear();
        foreach (var item in items)
        {
            Project? owner = null;
            if (item.ProjectId is not null)
            {
                projectLookup.TryGetValue(item.ProjectId, out owner);
            }

            tagLookup.TryGetValue(item.Id, out var assignedTags);
            _tasks.Add(new TaskRowViewModel(item, owner, assignedTags ?? new List<Tag>()));
        }

        await RefreshCountsAsync();
    }

    /// <summary>
    /// 刷新侧边栏活跃与已完成计数。
    /// </summary>
    /// <remarks>
    /// 原实现声明了 ActiveCount / CompletedCount 却从未赋值，侧边栏徽标恒显 0。
    /// 计数必须独立查询：当前筛选为"已完成"时，Tasks 集合内不含活跃项，
    /// 无法从中推导出活跃数。
    /// </remarks>
    private async Task RefreshCountsAsync()
    {
        var active = await _repository.GetAllActiveTasksAsync();
        var completed = await _repository.GetCompletedTasksAsync();

        ActiveCount = active.Count;
        CompletedCount = completed.Count;

        // 活动列表本身已含「已完成未归档」（完成 ≠ 归档），从中筛出待归档数，
        // 无需新增仓储查询方法
        PendingArchiveCount = active.Count(t => t.IsCompleted);
    }

    /// <summary>
    /// 新增任务。标题为空白时静默忽略，符合"回车即落入列表"的轻量交互预期。
    /// </summary>
    [RelayCommand]
    private async Task AddTaskAsync()
        => await new AddTaskViewModel(_repository, _tagRepository, _clock).ExecuteAsync(
            NewTaskTitle,
            NewTaskPriority,
            NewDueDateEditor.TakeValue(),
            NewTagChoices.Where(choice => choice.IsSelected).Select(choice => choice.Tag.Id),
            () =>
            {
                NewTaskTitle = string.Empty;
                NewDueDateEditor.Load(null);
                foreach (var choice in NewTagChoices)
                {
                    choice.IsSelected = false;
                }
            },
            CurrentSelection.Kind == ViewSelectionKind.Completed,
            () => CurrentSelection = ViewSelection.Active,
            LoadTasksAsync);

    /// <summary>
    /// 打开行编辑弹出层，用于修改到期日。
    /// </summary>
    [RelayCommand]
    private void OpenDueDatePopup(TaskRowViewModel? row)
        => new OpenDueDatePopupViewModel().Execute(
            row,
            target =>
            {
                EditingDueDateTarget = target;
                EditingDueDateEditor.Load(target.Task.DueDate, expandCalendar: true);
                IsDueDatePopupOpen = true;
            });

    /// <summary>
    /// 关闭到期日编辑弹出层，不保存更改。
    /// </summary>
    [RelayCommand]
    private void CloseDueDatePopup()
    {
        IsDueDatePopupOpen = false;
        EditingDueDateTarget = null;
    }

    /// <summary>
    /// 保存到期日编辑弹出层的更改。
    /// </summary>
    [RelayCommand]
    private async Task CommitDueDatePopupAsync()
        => await new CommitDueDatePopupViewModel(_repository).ExecuteAsync(
            EditingDueDateTarget,
            EditingDueDateEditor.TakeValue(),
            () =>
            {
                IsDueDatePopupOpen = false;
                EditingDueDateTarget = null;
            },
            LoadTasksAsync);

    /// <summary>
    /// 保存默认到期偏移设置。
    /// </summary>
    [RelayCommand]
    private async Task SaveDefaultDueOffsetAsync(int days)
    {
        if (!DueDateOffset.IsValid(days))
        {
            // 无效值时恢复到当前值
            return;
        }

        await _settingsRepository.SetDefaultDueOffsetDaysAsync(days);
    }

    /// <summary>
    /// 切换任务完成状态。
    /// </summary>
    /// <param name="item">目标任务；勾选框已通过双向绑定更新其 IsCompleted。</param>
    [RelayCommand]
    private async Task ToggleCompleteAsync(TaskItem? item)
        => await new ToggleCompleteTaskViewModel(_repository, _clock).ExecuteAsync(item, LoadTasksAsync);

    /// <summary>
    /// 手动归档全部已完成任务（spec-task-complete-before-archive D3：全局范围）。
    /// </summary>
    /// <remarks>
    /// 完成 ≠ 归档：勾选完成只是划线低饱和地留在活动列表；
    /// 用户需要显式点击这个动作才会真正移入「已完成归档」视图。
    /// </remarks>
    [RelayCommand]
    private async Task ArchiveCompletedAsync()
    {
        await _repository.ArchiveAllCompletedAsync();
        await LoadTasksAsync();
    }

    /// <summary>
    /// 软删除任务。
    /// </summary>
    /// <param name="item">目标任务。</param>
    /// <remarks>
    /// 参数类型从 <c>string</c> 改为 <see cref="TaskItem"/>：源生成器原先产出
    /// <c>RelayCommand&lt;string&gt;</c>，而视图传入的 CommandParameter 是 TaskItem 实例，
    /// 类型不匹配会在点击删除时抛出异常。签名与调用方对齐后消除该缺陷。
    /// </remarks>
    [RelayCommand]
    private async Task DeleteTaskAsync(TaskItem? item)
        => await new DeleteTaskViewModel(_repository).ExecuteAsync(item, LoadTasksAsync);

    /// <summary>
    /// 展开或收起某行的编辑面板。
    /// </summary>
    /// <remarks>
    /// 同一时刻只允许一行处于编辑态：多行同时展开会让任务流被面板撑满、
    /// 丧失 Editorial 排版的浏览性，也使「当前在改哪一条」变得不明确。
    /// </remarks>
    [RelayCommand]
    private async Task ToggleEditAsync(TaskRowViewModel? row)
        => await new ToggleEditTaskViewModel(
                new SaveEditTaskViewModel(_repository, _tagRepository))
            .ExecuteAsync(
                row,
                _tasks,
                ProjectChoices,
                _tags.Select(item => item.Tag),
                LoadTasksAsync);

    /// <summary>
    /// 提交某行的编辑缓冲并收起面板。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么没有「取消」</b>：本地 SQLite 写入是微秒级的，不存在需要用户等待的提交成本。
    /// 引入显式保存按钮反而带来「未保存状态」这一额外状态机，
    /// 以及「改了却忘记点保存」的数据丢失风险（design-domain-contract §5.3）。
    /// 代价是误改无法一键还原 —— 撤销栈的成本远高于其在此场景的收益，已明确排除。
    /// </para>
    /// <para>
    /// <b>非法标题的处理</b>：视为无效输入并丢弃该项修改（保留原标题），
    /// 而非拒绝整次提交 —— 否则用户改对了的日期与标签也会一并丢失。
    /// 校验规则与创建路径共用 <see cref="TaskTitle"/>，
    /// 同一约束不得在两处呈现不同行为（Article 6）。
    /// </para>
    /// </remarks>
    [RelayCommand]
    private async Task SaveEditAsync(TaskRowViewModel? row)
        => await new SaveEditTaskViewModel(_repository, _tagRepository)
            .ExecuteAsync(row, LoadTasksAsync);


    /// <summary>
    /// 变更任务所属项目。
    /// </summary>
    /// <param name="item">目标任务。</param>
    /// <param name="projectId">目标项目 Id；<c>null</c> 表示移出项目回到未归属状态。</param>
    public async Task AssignProjectAsync(TaskItem? item, string? projectId)
    {
        if (item is null)
        {
            return;
        }

        item.ProjectId = projectId;
        await _repository.SaveTaskAsync(item);
        await LoadTasksAsync();
    }

    /// <summary>
    /// 设置任务到期日。
    /// </summary>
    /// <param name="item">目标任务。</param>
    /// <param name="dueDate">到期日；<c>null</c> 表示清除，回到「未安排」状态。</param>
    /// <remarks>
    /// 传入值的时间部分由仓储统一归一化为当日零点，调用方无需自行处理。
    /// </remarks>
    public async Task SetDueDateAsync(TaskItem? item, DateTime? dueDate)
    {
        if (item is null)
        {
            return;
        }

        item.DueDate = dueDate;
        await _repository.SaveTaskAsync(item);
        await LoadTasksAsync();
    }

    // ==================== 项目管理 ====================

    /// <summary>新建项目的名称输入。</summary>
    [ObservableProperty]
    private string _newProjectName = string.Empty;

    /// <summary>新建项目输入区是否展开。</summary>
    /// <remarks>
    /// 依渐进披露原则（design-domain-contract §1 原则 3），新建入口默认收起，
    /// 不占用侧边栏空间，点击「＋」才展开。
    /// </remarks>
    [ObservableProperty]
    private bool _isCreatingProject;

    /// <summary>待确认删除的项目；<c>null</c> 表示无待确认操作。</summary>
    [ObservableProperty]
    private ProjectItemViewModel? _projectPendingDeletion;

    /// <summary>待删除项目影响的任务条数，用于确认提示。</summary>
    [ObservableProperty]
    private int _projectPendingDeletionTaskCount;

    /// <summary>展开或收起新建项目输入区。</summary>
    [RelayCommand]
    private void ToggleCreateProject()
    {
        IsCreatingProject = !IsCreatingProject;
        if (!IsCreatingProject)
        {
            NewProjectName = string.Empty;
        }
    }

    /// <summary>
    /// 创建项目。名称非法时静默忽略，与任务创建的交互预期一致。
    /// </summary>
    [RelayCommand]
    private async Task CreateProjectAsync()
        => await new CreateProjectViewModel(_projectRepository, _clock).ExecuteAsync(
            NewProjectName,
            _projects.Count,
            () =>
            {
                NewProjectName = string.Empty;
                IsCreatingProject = false;
            },
            LoadProjectsAsync);

    /// <summary>
    /// 将选中状态同步到各项目行，供侧边栏高亮绑定。
    /// </summary>
    /// <remarks>
    /// 直接读取 <see cref="CurrentSelection"/> 而非接收参数：
    /// 选中项目 Id 已是统一状态量的一部分，不再需要调用方另行传入
    /// （旧实现需要传参正是因为存在两份独立状态，见 SPEC §1.1）。
    /// </remarks>
    private void SyncProjectSelectionFlags()
    {
        var selectedId = CurrentSelection.Kind == ViewSelectionKind.Project
            ? CurrentSelection.ProjectId
            : null;

        foreach (var row in _projects)
        {
            row.IsSelected = row.Id == selectedId;
        }
    }

    /// <summary>进入项目重命名编辑态。</summary>
    [RelayCommand]
    private void BeginRenameProject(ProjectItemViewModel? project) => project?.BeginRename();

    /// <summary>放弃项目重命名。</summary>
    [RelayCommand]
    private void CancelRenameProject(ProjectItemViewModel? project) => project?.CancelRename();

    /// <summary>
    /// 提交项目重命名。名称非法时放弃修改并保留原名。
    /// </summary>
    [RelayCommand]
    private async Task CommitRenameProjectAsync(ProjectItemViewModel? project)
        => await new CommitRenameProjectViewModel(_projectRepository).ExecuteAsync(
            project,
            SelectedProject?.Id,
            title => CurrentCategoryTitle = title);

    /// <summary>
    /// 变更项目颜色。
    /// </summary>
    [RelayCommand]
    private async Task ChangeProjectColorAsync(ProjectItemViewModel? project)
        => await new ChangeProjectColorViewModel(_projectRepository).ExecuteAsync(project, LoadTasksAsync);

    /// <summary>归档项目。其下任务保留归属，仅从侧边栏隐去。</summary>
    /// <remarks>
    /// <b>为什么无需显式的 <c>wasSelected</c> 分支</b>：<see cref="LoadProjectsAsync"/>
    /// 已经统一处理「选中项目在重载后已不存在」的情形（发现该 Id 不在新集合中即
    /// 调用 <see cref="ReturnToActiveViewAsync"/>），归档与删除现在共用同一条回退路径，
    /// 不必在各自的命令方法里各自记一次 <c>wasSelected</c>（Article 10：同一决策只在一处表达）。
    /// 未选中该项目时，<see cref="LoadProjectsAsync"/> 不会变更 <see cref="CurrentSelection"/>，
    /// 因此仍需显式 <see cref="LoadTasksAsync"/> 以刷新侧边栏计数与任务流。
    /// </remarks>
    [RelayCommand]
    private async Task ArchiveProjectAsync(ProjectItemViewModel? project)
        => await new ArchiveProjectViewModel(_projectRepository).ExecuteAsync(
            project,
            CurrentSelection.Kind == ViewSelectionKind.Project
                && CurrentSelection.ProjectId == project?.Id,
            LoadProjectsAsync,
            LoadTasksAsync);

    /// <summary>
    /// 请求删除项目：先查询影响范围，交由界面确认。
    /// </summary>
    /// <remarks>
    /// 删除项目不可逆（<see cref="Project"/> 无软删除标记），
    /// 因此必须先告知将影响多少条任务再让用户决定（design-domain-contract §2.2）。
    /// </remarks>
    [RelayCommand]
    private async Task RequestDeleteProjectAsync(ProjectItemViewModel? project)
        => await new RequestDeleteProjectViewModel(_projectRepository).ExecuteAsync(
            project,
            (pending, count) =>
            {
                ProjectPendingDeletion = pending;
                ProjectPendingDeletionTaskCount = count;
            });

    /// <summary>撤销删除确认。</summary>
    [RelayCommand]
    private void CancelDeleteProject() => ProjectPendingDeletion = null;

    /// <summary>
    /// 确认删除项目。
    /// </summary>
    /// <remarks>
    /// <b>其下任务不会被删除</b>，仅 <c>ProjectId</c> 置空退回未归属状态 ——
    /// 任务是用户的核心资产，项目只是它的一个可选属性（design-domain-contract §2.2）。
    /// 该语义由 <c>SqliteProjectRepository.DeleteAsync</c> 以单事务保证。
    /// <para>
    /// <b>回退逻辑同 <see cref="ArchiveProjectAsync"/></b>：由 <see cref="LoadProjectsAsync"/>
    /// 统一判定选中项目是否仍存在，无需在此重复记录 <c>wasSelected</c> 后再调用一次
    /// <see cref="ReturnToActiveViewAsync"/>。
    /// </para>
    /// </remarks>
    [RelayCommand]
    private async Task ConfirmDeleteProjectAsync()
    {
        var target = ProjectPendingDeletion;
        await new ConfirmDeleteProjectViewModel(_projectRepository).ExecuteAsync(
            target,
            CurrentSelection.Kind == ViewSelectionKind.Project
                && CurrentSelection.ProjectId == target?.Id,
            () => ProjectPendingDeletion = null,
            LoadProjectsAsync,
            LoadTasksAsync);
    }

    /// <summary>
    /// 唤起随手记浮窗。
    /// </summary>
    [RelayCommand]
    private void OpenQuickCapture() => RequestOpenQuickCapture?.Invoke();

    /// <summary>
    /// 应用主题变体。视图层在水波纹扩散覆盖屏幕后调用，实现无闪烁换肤。
    /// </summary>
    /// <param name="isDark">是否切换至深色。</param>
    public void ApplyTheme(bool isDark)
    {
        IsDarkTheme = isDark;
        AppearanceCoordinator.ApplyTheme(isDark);
        ThemeApplied?.Invoke();
    }

    /// <summary>
    /// 反转当前昼夜主题。
    /// </summary>
    [RelayCommand]
    private void ToggleTheme() => ApplyTheme(!IsDarkTheme);

    /// <summary>
    /// 展开或收起外观设置视图。
    /// </summary>
    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    // ==================== 标签管理 ====================

    /// <summary>新建标签。名称为空或重复时不写入数据库。</summary>
    [RelayCommand]
    private async Task CreateTagAsync()
        => await new CreateTagViewModel(_tagRepository, _clock).ExecuteAsync(
            NewTagName,
            _tags.Select(item => item.Name),
            _tags.Count,
            () => NewTagName = string.Empty,
            LoadTagsAsync);

    /// <summary>进入标签重命名编辑态。</summary>
    [RelayCommand]
    private void BeginRenameTag(TagItemViewModel? tag) => tag?.BeginRename();

    /// <summary>取消标签重命名。</summary>
    [RelayCommand]
    private void CancelRenameTag(TagItemViewModel? tag) => tag?.CancelRename();

    /// <summary>提交标签重命名，并保留所有任务关联。</summary>
    [RelayCommand]
    private async Task CommitRenameTagAsync(TagItemViewModel? tag)
        => await new CommitRenameTagViewModel(_tagRepository).ExecuteAsync(
            tag,
            _tags.Select(item => (item.Id, item.Name)),
            LoadTasksAsync);

    /// <summary>循环切换标签色。</summary>
    [RelayCommand]
    private async Task ChangeTagColorAsync(TagItemViewModel? tag)
        => await new ChangeTagColorViewModel(_tagRepository).ExecuteAsync(
            tag,
            LoadTagsAsync,
            LoadTasksAsync);

    /// <summary>删除标签，同时清理所有任务关联。</summary>
    [RelayCommand]
    private async Task DeleteTagAsync(TagItemViewModel? tag)
        => await new DeleteTagViewModel(_tagRepository).ExecuteAsync(
            tag,
            LoadTagsAsync,
            LoadTasksAsync);

    /// <summary>
    /// 材质预设选中变更时立即请求视图层应用，无需额外的确认命令。
    /// </summary>
    partial void OnSelectedMaterialChanged(MaterialOption value) => MaterialPresetChanged?.Invoke(value);

    /// <summary>
    /// 强调色预设选中变更时立即写入主题字典，实现即时生效。
    /// </summary>
    partial void OnSelectedAccentChanged(AppearanceOption value) => AppearanceCoordinator.ApplyAccent(value.Id);

    /// <summary>
    /// 手动刷新任务流。
    /// </summary>
    [RelayCommand]
    private async Task RefreshTasksAsync() => await LoadTasksAsync();

    /// <inheritdoc />
    public void Receive(TaskSavedMessage message)
        => Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = RefreshAfterCaptureAsync());

    /// <inheritdoc />
    public void Receive(TaskDeletedMessage message)
        => Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = LoadTasksAsync());

    /// <summary>
    /// 小窗可能在保存时新建项目/标签，须连同侧边栏一并刷新。
    /// </summary>
    private async Task RefreshAfterCaptureAsync()
    {
        await LoadTagsAsync();
        await LoadProjectsAsync();
        await LoadTasksAsync();
    }
}
