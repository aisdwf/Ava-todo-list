using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FlowTask.Core.Enums;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Messages;
using FlowTask.Core.Models;
using FlowTask.Desktop.Appearance;

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

    /// <summary>今日到期任务。</summary>
    Today,

    /// <summary>已完成归档。</summary>
    Completed
}

/// <summary>
/// 主工作台视图模型：承载 Editorial 任务流、视图筛选与外观个性化状态。
/// </summary>
public partial class MainViewModel : ViewModelBase, IRecipient<TaskSavedMessage>, IRecipient<TaskDeletedMessage>
{
    private readonly ITaskRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly IClock _clock;

    /// <summary>
    /// 抑制筛选属性联动时的重复加载。
    /// </summary>
    /// <remarks>
    /// 为什么需要：三个 <c>IsXxxSelected</c> 布尔量与 <c>CurrentFilter</c> 互相赋值，
    /// 原实现在每个 partial 变更回调里各自触发一次 <c>LoadTasksAsync</c>，
    /// 单次点击导航会连发 2-3 次数据库查询并多次清空重填集合，造成列表闪烁。
    /// </remarks>
    private bool _isSyncingFilter;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    /// <summary>
    /// 设置视图是否展开。与任务筛选正交，因此不占用 <see cref="TaskFilter"/> 枚举位。
    /// </summary>
    [ObservableProperty]
    private bool _isSettingsOpen;

    /// <summary>
    /// 当前选中的项目；<c>null</c> 表示未启用项目筛选，此时由 <see cref="CurrentFilter"/> 决定视图。
    /// </summary>
    /// <remarks>
    /// <b>为什么不塞进 <see cref="TaskFilter"/> 枚举</b>：项目筛选与 VIEWS 筛选是两个正交维度 ——
    /// 枚举只能表达「三选一」，而项目数量动态、且「未选中任何项目」也是合法状态。
    /// 该枚举此前曾混入「设置页」导致视图模式与数据筛选耦合，已在 spec-editorial-and-ripple-theme 修正；
    /// 此处不得重犯同类错误（design-domain-contract §5.1）。
    /// </remarks>
    [ObservableProperty]
    private Project? _selectedProject;

    /// <summary>
    /// 当前窗口材质预设。以对象而非 Id 字符串持有，直接充当 ListBox 的 SelectedItem，
    /// 省去一层 Id 到对象的查找映射。
    /// </summary>
    [ObservableProperty]
    private MaterialOption _selectedMaterial = AppearanceCoordinator.MaterialPresets[0];

    /// <summary>当前强调色预设。</summary>
    [ObservableProperty]
    private AppearanceOption _selectedAccent = AppearanceCoordinator.AccentPresets[0];

    [ObservableProperty]
    private TaskFilter _currentFilter = TaskFilter.Active;

    [ObservableProperty]
    private string _currentCategoryTitle = "全部任务";

    [ObservableProperty]
    private string _currentCategorySubtitle = "聚焦所有活跃进行中的待办";

    [ObservableProperty]
    private bool _isActiveFilterSelected = true;

    [ObservableProperty]
    private bool _isTodayFilterSelected;

    [ObservableProperty]
    private bool _isCompletedFilterSelected;

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    /// <summary>新任务的优先级档位，默认中优先级。</summary>
    [ObservableProperty]
    private TaskPriority _newTaskPriority = TaskPriority.Medium;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _completedCount;

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

    /// <summary>请求应用窗口材质。窗口实例归视图层所有，故以事件外发。</summary>
    public event Action<MaterialOption>? MaterialPresetChanged;

    /// <summary>主题变体已应用，通知视图层重建依赖具体笔刷实例的视觉属性。</summary>
    public event Action? ThemeApplied;

    /// <summary>
    /// 构造主视图模型。
    /// </summary>
    /// <param name="repository">任务仓储。</param>
    /// <param name="projectRepository">项目仓储。</param>
    /// <param name="clock">时间提供者，用于显式赋值任务的创建与完成时刻（Article 9）。</param>
    public MainViewModel(ITaskRepository repository, IProjectRepository projectRepository, IClock clock)
    {
        _repository = repository;
        _projectRepository = projectRepository;
        _clock = clock;

        Tasks = new ReadOnlyObservableCollection<TaskRowViewModel>(_tasks);
        Projects = new ReadOnlyObservableCollection<ProjectItemViewModel>(_projects);

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
        AppearanceCoordinator.ApplyAccent(SelectedAccent.Id);
        await LoadProjectsAsync();
        await LoadTasksAsync();
    }

    /// <summary>
    /// 载入未归档项目及其任务计数，并同步编辑态下拉候选。
    /// </summary>
    private async Task LoadProjectsAsync()
    {
        var projects = await _projectRepository.GetActiveProjectsAsync();

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
        // 经 ReturnToActiveViewAsync 显式重建状态，而非赋值 CurrentFilter ——
        // 后者在新值等于旧值时不会触发回调，单选高亮将无法恢复
        if (SelectedProject is not null
            && _projects.All(p => p.Id != SelectedProject.Id))
        {
            await ReturnToActiveViewAsync();
            return;
        }

        // 集合已重建为新实例，须把选中标志重新贴回对应行，否则高亮丢失
        SyncProjectSelectionFlags(SelectedProject?.Id);
    }

    partial void OnCurrentFilterChanged(TaskFilter value)
    {
        // 选择 VIEWS 筛选即解除项目筛选：二者正交但在界面上互斥，
        // 同时高亮两处会让用户无法判断当前看的是哪个集合
        if (!_isSyncingFilter)
        {
            SelectedProject = null;
        }

        (CurrentCategoryTitle, CurrentCategorySubtitle) = value switch
        {
            TaskFilter.Today => ("今日聚焦", "今天需要优先解决的关键事项"),
            TaskFilter.Completed => ("已完成归档", "所有已达成的历史成果记录"),
            _ => ("全部任务", "聚焦所有活跃进行中的待办")
        };

        _isSyncingFilter = true;
        IsActiveFilterSelected = value == TaskFilter.Active;
        IsTodayFilterSelected = value == TaskFilter.Today;
        IsCompletedFilterSelected = value == TaskFilter.Completed;
        _isSyncingFilter = false;

        // 切换筛选即离开设置页，避免用户点击导航后内容区无响应
        IsSettingsOpen = false;

        _ = LoadTasksAsync();
    }

    /// <summary>
    /// 选中项目并切换到该项目的任务列表。
    /// </summary>
    /// <param name="project">目标项目行；传 <c>null</c> 回到「全部任务」。</param>
    [RelayCommand]
    private async Task SelectProjectAsync(ProjectItemViewModel? project)
    {
        IsSettingsOpen = false;

        if (project is null)
        {
            await ReturnToActiveViewAsync();
            return;
        }

        SelectedProject = project.Project;
        SyncProjectSelectionFlags(project.Id);

        // 解除 VIEWS 三个单选的高亮：项目筛选生效时它们都不该显示为选中。
        // 借用既有的 _isSyncingFilter 抑制回环，避免另起一套防重载机制
        _isSyncingFilter = true;
        IsActiveFilterSelected = false;
        IsTodayFilterSelected = false;
        IsCompletedFilterSelected = false;
        _isSyncingFilter = false;

        CurrentCategoryTitle = project.Name;
        CurrentCategorySubtitle = "该项目下进行中的待办";

        await LoadTasksAsync();
    }

    /// <summary>
    /// 解除项目筛选，回到「全部任务」视图。
    /// </summary>
    /// <remarks>
    /// <b>为什么不能简单写 <c>CurrentFilter = TaskFilter.Active</c></b>：
    /// 项目筛选生效期间 <see cref="CurrentFilter"/> 仍停留在其原值（通常就是
    /// <see cref="TaskFilter.Active"/>），仅三个 <c>IsXxxFilterSelected</c> 被清空。
    /// 此时赋同值属性不变更、<c>OnCurrentFilterChanged</c> 不触发，
    /// 单选高亮便永远无法恢复 —— 侧边栏会呈现「没有任何项被选中」的空档状态。
    /// <para>
    /// 这与 spec-editorial-and-ripple-theme 修正过的缺陷同源（见 <see cref="ChangeFilter"/> 注释）：
    /// <b>依赖属性变更回调来同步状态，在「新值等于旧值」时必然失效。</b>
    /// 因此此处显式重建状态，不经由属性变更通知这条路径。
    /// </para>
    /// </remarks>
    private async Task ReturnToActiveViewAsync()
    {
        SelectedProject = null;
        SyncProjectSelectionFlags(null);
        CurrentFilter = TaskFilter.Active;

        _isSyncingFilter = true;
        IsActiveFilterSelected = true;
        IsTodayFilterSelected = false;
        IsCompletedFilterSelected = false;
        _isSyncingFilter = false;

        CurrentCategoryTitle = "全部任务";
        CurrentCategorySubtitle = "聚焦所有活跃进行中的待办";

        await LoadTasksAsync();
    }

    partial void OnIsActiveFilterSelectedChanged(bool value) => SyncFilterFromRadio(value, TaskFilter.Active);

    partial void OnIsTodayFilterSelectedChanged(bool value) => SyncFilterFromRadio(value, TaskFilter.Today);

    partial void OnIsCompletedFilterSelectedChanged(bool value) => SyncFilterFromRadio(value, TaskFilter.Completed);

    /// <summary>
    /// 将单选按钮的勾选状态回写为筛选枚举，屏蔽枚举驱动时的反向回环。
    /// </summary>
    private void SyncFilterFromRadio(bool isChecked, TaskFilter filter)
    {
        if (_isSyncingFilter || !isChecked)
        {
            return;
        }

        // 与 ChangeFilter 同样需要显式离开设置页：左侧导航走 IsChecked 双向绑定路径，
        // 若目标筛选已是当前值，CurrentFilter 不变更、回调不触发。
        IsSettingsOpen = false;
        CurrentFilter = filter;
    }

    /// <summary>
    /// 切换任务筛选维度。
    /// </summary>
    /// <remarks>
    /// 关闭设置页在此显式执行，而不能只依赖 <c>OnCurrentFilterChanged</c>：
    /// 当目标筛选与当前值相同时属性不发生变更、回调不触发，
    /// 用户在设置页点击当前已选中的导航项会得不到任何响应。
    /// </remarks>
    [RelayCommand]
    private void ChangeFilter(TaskFilter filter)
    {
        IsSettingsOpen = false;
        CurrentFilter = filter;
    }

    /// <summary>
    /// 按当前筛选载入任务，并同步侧边栏计数。
    /// </summary>
    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        // 项目筛选优先于 VIEWS 筛选：二者在界面上互斥，
        // 选中项目时 VIEWS 单选已被解除高亮，此处的分支顺序须与之一致
        var items = SelectedProject is not null
            ? await _repository.GetTasksByProjectAsync(SelectedProject.Id)
            : CurrentFilter switch
            {
                TaskFilter.Today => await _repository.GetTodayTasksAsync(),
                TaskFilter.Completed => await _repository.GetCompletedTasksAsync(),
                _ => await _repository.GetAllActiveTasksAsync()
            };

        // 建立 Id → 项目 的查找表，避免为每行任务各查一次库（N+1 查询）
        var projectLookup = _projects.ToDictionary(p => p.Id, p => p.Project);

        _tasks.Clear();
        foreach (var item in items)
        {
            Project? owner = null;
            if (item.ProjectId is not null)
            {
                projectLookup.TryGetValue(item.ProjectId, out owner);
            }

            _tasks.Add(new TaskRowViewModel(item, owner));
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
    }

    /// <summary>
    /// 新增任务。标题为空白时静默忽略，符合"回车即落入列表"的轻量交互预期。
    /// </summary>
    [RelayCommand]
    private async Task AddTaskAsync()
    {
        // 经 TaskTitle 而非各自判空：三处入口（此处、编辑态、随手记）
        // 共用同一规则，含长度上限（Article 6）
        if (!TaskTitle.IsValid(NewTaskTitle))
        {
            return;
        }

        // 经 TaskItemFactory 创建而非直接 new：创建时刻赋值与标签规范化等不变量
        // 集中在单一入口，避免主窗口与小窗各写一份而漂移（Article 6）
        var task = TaskItemFactory.Create(
            _clock,
            NewTaskTitle,
            NewTaskPriority);

        await _repository.SaveTaskAsync(task);
        NewTaskTitle = string.Empty;

        // 在"已完成"视图下新增的任务属于活跃集，留在原视图会让用户以为添加失败
        if (CurrentFilter == TaskFilter.Completed)
        {
            CurrentFilter = TaskFilter.Active;
            return;
        }

        await LoadTasksAsync();
    }

    /// <summary>
    /// 切换任务完成状态。
    /// </summary>
    /// <param name="item">目标任务；勾选框已通过双向绑定更新其 IsCompleted。</param>
    [RelayCommand]
    private async Task ToggleCompleteAsync(TaskItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.CompletedAt = item.IsCompleted ? _clock.UtcNow : null;
        await _repository.SaveTaskAsync(item);
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
    {
        if (item is null)
        {
            return;
        }

        await _repository.SoftDeleteAsync(item.Id);
        await LoadTasksAsync();
    }

    /// <summary>
    /// 展开或收起某行的编辑面板。
    /// </summary>
    /// <remarks>
    /// 同一时刻只允许一行处于编辑态：多行同时展开会让任务流被面板撑满、
    /// 丧失 Editorial 排版的浏览性，也使「当前在改哪一条」变得不明确。
    /// </remarks>
    [RelayCommand]
    private async Task ToggleEditAsync(TaskRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (row.IsEditing)
        {
            await SaveEditAsync(row);
            return;
        }

        // 收起其他行并提交它们的改动，避免未落库的编辑被静默丢弃
        foreach (var other in _tasks.Where(r => r.IsEditing && r != row).ToList())
        {
            await SaveEditAsync(other);
        }

        row.BeginEdit(ProjectChoices);
    }

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
    {
        if (row is null)
        {
            return;
        }

        var task = row.Task;

        // 标题非法时保留原值，其余字段的修改照常生效
        if (TaskTitle.IsValid(row.EditTitle))
        {
            task.Title = TaskTitle.Normalize(row.EditTitle);
        }

        task.Tags = TagNormalizer.Normalize(row.EditTags);
        task.Priority = row.EditPriority;
        task.ProjectId = row.EditProject.ProjectId;
        task.DueDate = ParseDueDate(row.EditDueDate);

        await _repository.SaveTaskAsync(task);

        row.EndEdit();
        await LoadTasksAsync();
    }

    /// <summary>
    /// 严格解析编辑态输入的到期日文本。
    /// </summary>
    /// <remarks>
    /// 用 <c>TryParseExact</c> 而非 <c>TryParse</c>：后者会按当前区域文化
    /// 接受「3/10」「March 10」等多种形态，同一串输入在不同机器上
    /// 可能解析出不同日期。严格格式让行为可预测。
    /// 无法解析时返回 <c>null</c>（视为清除到期日），而非抛异常打断保存。
    /// </remarks>
    private static DateTime? ParseDueDate(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return DateTime.TryParseExact(
            trimmed,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

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
    {
        if (!ProjectName.IsValid(NewProjectName))
        {
            return;
        }

        var project = new Project
        {
            Name = ProjectName.Normalize(NewProjectName),
            // 新项目排在末位：以当前项目数作为序号，避免与既有项目争位
            SortOrder = _projects.Count,
            ColorHex = PickNextProjectColor(),
            CreatedAt = _clock.UtcNow
        };

        await _projectRepository.SaveProjectAsync(project);

        NewProjectName = string.Empty;
        IsCreatingProject = false;

        await LoadProjectsAsync();
    }

    /// <summary>
    /// 为新项目挑选一个默认色。
    /// </summary>
    /// <remarks>
    /// 按项目数循环取用调色板，使相邻新建的项目自然获得不同颜色 ——
    /// 若全部默认同色，项目色条就失去了区分作用。
    /// 调色板取自既有强调色预设，避免另立一套颜色词汇（Article 6）。
    /// </remarks>
    /// <remarks>
    /// 取 <c>DarkHex</c> 而非 <c>Swatch.ToString()</c>：后者返回笔刷对象的字符串形式
    /// （可为 null，且格式不保证是十六进制色值），而 <c>ColorHex</c> 字段需要可解析的色值。
    /// 且 <c>Swatch</c> 是 UI 线程绑定的 <c>SolidColorBrush</c>，
    /// ViewModel 层不应为取一个色值而触达它。
    /// </remarks>
    private string PickNextProjectColor()
    {
        var palette = AppearanceCoordinator.AccentPresets;
        return palette[_projects.Count % palette.Count].DarkHex;
    }

    /// <summary>
    /// 将选中状态同步到各项目行，供侧边栏高亮绑定。
    /// </summary>
    /// <param name="selectedId">选中项目 Id；<c>null</c> 表示全部取消选中。</param>
    private void SyncProjectSelectionFlags(string? selectedId)
    {
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
    {
        if (project is null)
        {
            return;
        }

        if (!ProjectName.IsValid(project.RenameBuffer))
        {
            project.CancelRename();
            return;
        }

        project.Project.Name = ProjectName.Normalize(project.RenameBuffer);
        await _projectRepository.SaveProjectAsync(project.Project);

        // 就地同步而非重载整个集合：重载会丢失侧边栏选中态
        project.SyncFromEntity();
        project.CancelRename();

        // 选中项目被重命名时，标题区需同步
        if (SelectedProject?.Id == project.Id)
        {
            CurrentCategoryTitle = project.Name;
        }
    }

    /// <summary>
    /// 变更项目颜色。
    /// </summary>
    [RelayCommand]
    private async Task ChangeProjectColorAsync(ProjectItemViewModel? project)
    {
        if (project is null)
        {
            return;
        }

        // 在调色板中轮转到下一色：项目属性仅名称与颜色两项，
        // 为改色单独开一个取色器界面不成比例（渐进披露）。
        // 以 DarkHex 比对而非 Swatch —— 同 PickNextProjectColor 的理由
        var palette = AppearanceCoordinator.AccentPresets;
        var currentIndex = palette
            .Select((option, index) => (option, index))
            .FirstOrDefault(pair => string.Equals(
                pair.option.DarkHex, project.ColorHex, StringComparison.OrdinalIgnoreCase))
            .index;

        project.Project.ColorHex = palette[(currentIndex + 1) % palette.Count].DarkHex;
        await _projectRepository.SaveProjectAsync(project.Project);
        project.SyncFromEntity();

        // 任务行的项目色条依赖该颜色，须重载任务流才能刷新
        await LoadTasksAsync();
    }

    /// <summary>归档项目。其下任务保留归属，仅从侧边栏隐去。</summary>
    [RelayCommand]
    private async Task ArchiveProjectAsync(ProjectItemViewModel? project)
    {
        if (project is null)
        {
            return;
        }

        var wasSelected = SelectedProject?.Id == project.Id;

        await _projectRepository.SetArchivedAsync(project.Id, true);
        await LoadProjectsAsync();

        // 归档当前选中项目后须显式重建视图状态，
        // 不能依赖属性变更回调（新值可能等于旧值，见 ReturnToActiveViewAsync）
        if (wasSelected)
        {
            await ReturnToActiveViewAsync();
        }
        else
        {
            await LoadTasksAsync();
        }
    }

    /// <summary>
    /// 请求删除项目：先查询影响范围，交由界面确认。
    /// </summary>
    /// <remarks>
    /// 删除项目不可逆（<see cref="Project"/> 无软删除标记），
    /// 因此必须先告知将影响多少条任务再让用户决定（design-domain-contract §2.2）。
    /// </remarks>
    [RelayCommand]
    private async Task RequestDeleteProjectAsync(ProjectItemViewModel? project)
    {
        if (project is null)
        {
            return;
        }

        ProjectPendingDeletionTaskCount = await _projectRepository.CountTasksAsync(project.Id);
        ProjectPendingDeletion = project;
    }

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
    /// </remarks>
    [RelayCommand]
    private async Task ConfirmDeleteProjectAsync()
    {
        var target = ProjectPendingDeletion;
        if (target is null)
        {
            return;
        }

        var wasSelected = SelectedProject?.Id == target.Id;

        await _projectRepository.DeleteAsync(target.Id);
        ProjectPendingDeletion = null;

        await LoadProjectsAsync();

        if (wasSelected)
        {
            await ReturnToActiveViewAsync();
        }
        else
        {
            await LoadTasksAsync();
        }
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
        => Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = LoadTasksAsync());

    /// <inheritdoc />
    public void Receive(TaskDeletedMessage message)
        => Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = LoadTasksAsync());
}
