using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FlowTask.Core.Enums;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Messages;
using FlowTask.Core.Models;
using FlowTask.Core.Ordering;
using FlowTask.Desktop.Services;
using FlowTask.Desktop.ViewModels.Actions;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 快捷小窗：单项目列表 + 勾选 + 录入。项目下拉是唯一的项目上下文，
/// 列表展示与新建归属都跟随它（spec-quick-window-single-project-list §7，R-1.9）。
/// </summary>
public partial class QuickCaptureViewModel : ViewModelBase,
    IRecipient<TaskSavedMessage>, IRecipient<TaskDeletedMessage>, IRecipient<ProjectsChangedMessage>
{
    /// <summary>记忆上次在小窗选择项目的设置键（spec-quick-window-single-project-list §2.4）。</summary>
    private const string LastProjectSettingsKey = "QuickWindow.LastProjectId";

    private readonly ITaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly IClock _clock;

    private List<Project> _projects = [];

    /// <summary>切换项目时抑制联动查询，避免 <see cref="PrepareAsync"/> 恢复上次项目时触发一次多余的重复加载。</summary>
    private bool _suppressProjectSelectionReload;

    /// <summary>
    /// 本窗口自己发出的总线消息通过 Origin 跳过，避免与命令内的 reload 并发重建同一集合。
    /// </summary>
    private int _tasksLoadGeneration;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private TaskPriority _priority = TaskPriority.Medium;

    /// <summary>
    /// 项目下拉候选（含系统 Default），驱动小窗单项目列表切换（D1：下拉）。
    /// </summary>
    public ObservableCollection<ProjectItemViewModel> Projects { get; } = [];

    /// <summary>当前选中项目；驱动任务列表查询、新建归属与「记忆上次项目」持久化。</summary>
    [ObservableProperty]
    private ProjectItemViewModel? _selectedProject;

    /// <summary>
    /// 当前选中项目下的任务（单项目列表，spec-quick-window-single-project-list）。
    /// </summary>
    /// <remarks>直接复用主窗 <see cref="TaskRowViewModel"/>，勾选走与主窗同一套完成命令，
    /// 避免重复实现完成套件（spec-project-managed-tasks）。</remarks>
    public ObservableCollection<TaskRowViewModel> Tasks { get; } = [];

    /// <summary>列表是否为空，驱动空状态提示显隐。</summary>
    public bool IsTaskListEmpty => Tasks.Count == 0;

    /// <summary>
    /// 最近一次由 <see cref="TaskSavedMessage"/> 触发的列表重载。
    /// 对端勾选后测试等待本任务，避免用延时猜测 UI 刷新（Article 9）。
    /// </summary>
    public Task TaskListRefreshTask { get; private set; } = Task.CompletedTask;

    /// <summary>请求关闭浮窗。由视图层订阅，ViewModel 不持有窗口引用。</summary>
    public event Action? RequestClose;

    /// <summary>
    /// 构造快捷小窗视图模型。
    /// </summary>
    public QuickCaptureViewModel(
        ITaskRepository taskRepository,
        IProjectRepository projectRepository,
        IAppSettingsRepository settingsRepository,
        IClock clock)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _settingsRepository = settingsRepository;
        _clock = clock;

        WeakReferenceMessenger.Default.Register<TaskSavedMessage>(this);
        WeakReferenceMessenger.Default.Register<TaskDeletedMessage>(this);
        WeakReferenceMessenger.Default.Register<ProjectsChangedMessage>(this);
    }

    /// <summary>
    /// 打开浮窗前刷新项目缓存，恢复上次选中项目并载入其任务列表。
    /// </summary>
    public async Task PrepareAsync()
    {
        await _projectRepository.EnsureDefaultProjectAsync(_clock.UtcNow);
        await ReloadActiveProjectsAsync();

        var lastProjectId = await _settingsRepository.GetAsync(LastProjectSettingsKey);
        RebuildProjectChoices(lastProjectId);
        await LoadTasksForSelectedProjectAsync();
    }

    private async Task ReloadActiveProjectsAsync()
    {
        var all = await _projectRepository.GetAllProjectsAsync();
        _projects = all.Where(p => !p.IsArchived).OrderBy(p => p.SortOrder).ToList();
    }

    /// <summary>
    /// 按当前内存中的活跃项目重建下拉，并选中 <paramref name="preferredProjectId"/>
    /// （已不存在则回退 Default）。
    /// </summary>
    private void RebuildProjectChoices(string? preferredProjectId)
    {
        Projects.Clear();
        foreach (var project in _projects)
        {
            Projects.Add(new ProjectItemViewModel(project, 0));
        }

        var restored = Projects.FirstOrDefault(p => p.Id == preferredProjectId)
                       ?? Projects.FirstOrDefault(p => p.Id == DefaultProject.Id);

        SelectProjectSilently(restored);
    }

    /// <summary>改选中项但不触发 <see cref="OnSelectedProjectChanged"/> 的 fire-and-forget 联动。</summary>
    private void SelectProjectSilently(ProjectItemViewModel? project)
    {
        _suppressProjectSelectionReload = true;
        try
        {
            SelectedProject = project;
        }
        finally
        {
            _suppressProjectSelectionReload = false;
        }
    }

    /// <summary>
    /// 项目切换：写回记忆项并重新查询任务列表（D1）。
    /// </summary>
    /// <remarks>
    /// 属性变更回调本身不能是 <c>async</c>，故以 fire-and-forget 转发到
    /// <see cref="ChangeSelectedProjectCommand"/>——后者是可显式 <c>await</c> 的确定入口，
    /// 供测试与「记忆上次项目」两部分写入（设置 + 任务重载）一起等待完成，
    /// 与主窗 <c>MainViewModel.LoadTasksCommand</c> 的既有测试模式一致。
    /// </remarks>
    partial void OnSelectedProjectChanged(ProjectItemViewModel? value)
    {
        if (_suppressProjectSelectionReload)
        {
            return;
        }

        LoggedTasks.FireAndForget(
            ChangeSelectedProjectCommand.ExecuteAsync(value),
            "QuickCapture OnSelectedProjectChanged");
    }

    [RelayCommand]
    private async Task ChangeSelectedProjectAsync(ProjectItemViewModel? value)
    {
        await _settingsRepository.SetAsync(LastProjectSettingsKey, value?.Id ?? DefaultProject.Id);
        await LoadTasksForSelectedProjectAsync();
    }

    /// <summary>切到下一个项目，末尾回到首项（D6：<c>Ctrl+Tab</c>）。</summary>
    [RelayCommand]
    private Task SelectNextProjectAsync() => CycleProjectAsync(1);

    /// <summary>切到上一个项目，首项回到末尾（D6：<c>Ctrl+Shift+Tab</c>）。</summary>
    [RelayCommand]
    private Task SelectPreviousProjectAsync() => CycleProjectAsync(-1);

    /// <remarks>
    /// 静默改选中项后直接 await 切换命令，而不是依赖属性回调的 fire-and-forget，
    /// 使键盘切换的调用方（及测试）能拿到确定的完成点。
    /// </remarks>
    private async Task CycleProjectAsync(int step)
    {
        if (Projects.Count == 0)
        {
            return;
        }

        var current = SelectedProject is null ? 0 : Math.Max(0, Projects.IndexOf(SelectedProject));
        var next = Projects[((current + step) % Projects.Count + Projects.Count) % Projects.Count];

        SelectProjectSilently(next);
        await ChangeSelectedProjectAsync(next);
    }

    /// <summary>
    /// 按当前选中项目载入任务，排序与主窗共用 <see cref="TaskListOrder"/>。
    /// </summary>
    private async Task LoadTasksForSelectedProjectAsync()
    {
        var generation = Interlocked.Increment(ref _tasksLoadGeneration);
        var projectId = SelectedProject?.Id ?? DefaultProject.Id;
        var items = await _taskRepository.GetTasksByProjectAsync(projectId);
        if (generation != Volatile.Read(ref _tasksLoadGeneration))
        {
            return;
        }

        var ordered = TaskListOrder.Sort(items);

        var project = _projects.FirstOrDefault(p => p.Id == projectId);

        Tasks.Clear();
        foreach (var item in ordered)
        {
            Tasks.Add(new TaskRowViewModel(item, project));
        }

        OnPropertyChanged(nameof(IsTaskListEmpty));
    }

    /// <summary>
    /// 切换任务完成状态。与主窗共用完成套件：勾选后任务仍留在列表并置底。
    /// </summary>
    [RelayCommand]
    private async Task ToggleTaskCompleteAsync(TaskItem? item)
        => await new ToggleCompleteTaskViewModel(_taskRepository, _clock)
            .ExecuteAsync(item, LoadTasksForSelectedProjectAsync, this);

    /// <inheritdoc />
    public void Receive(TaskSavedMessage message)
    {
        if (ReferenceEquals(message.Origin, this))
        {
            return;
        }

        TaskListRefreshTask = UiThread.RunAsync(() => SyncFromStoreAsync(message.Task.Id));
    }

    /// <inheritdoc />
    public void Receive(TaskDeletedMessage message)
    {
        if (ReferenceEquals(message.Origin, this))
        {
            return;
        }

        TaskListRefreshTask = UiThread.RunAsync(LoadTasksForSelectedProjectAsync);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 小窗隐藏时也会收到并刷新：实例被复用（Hide 而非 Close），
    /// 下次打开前 <see cref="PrepareAsync"/> 仍会重读，这里保证可见期间实时同步。
    /// </remarks>
    public void Receive(ProjectsChangedMessage message)
    {
        if (ReferenceEquals(message.Origin, this))
        {
            return;
        }

        TaskListRefreshTask = UiThread.RunAsync(HandlePeerProjectsChangedAsync);
    }

    /// <summary>
    /// 对端新建 / 重命名 / 归档 / 删除项目后：重读项目表并重建下拉，保留当前选中项。
    /// 若当前选中项目已不存在（归档或删除），回退 Default 并写回记忆项（与 <see cref="PrepareAsync"/> 一致）。
    /// </summary>
    private async Task HandlePeerProjectsChangedAsync()
    {
        var preferred = SelectedProject?.Id;
        await ReloadActiveProjectsAsync();
        RebuildProjectChoices(preferred);
        if (SelectedProject?.Id != preferred)
        {
            await _settingsRepository.SetAsync(
                LastProjectSettingsKey,
                SelectedProject?.Id ?? DefaultProject.Id);
        }

        // 行上的项目名/色取自 _projects 快照，重命名后须重建行
        await LoadTasksForSelectedProjectAsync();
    }

    /// <summary>
    /// 从 SQLite 读回该任务并就地写到当前小窗列表里已有的行。
    /// </summary>
    private async Task SyncFromStoreAsync(string taskId)
    {
        var persisted = await _taskRepository.GetByIdAsync(taskId);
        if (persisted is null)
        {
            await LoadTasksForSelectedProjectAsync();
            return;
        }

        if (!TaskRowListSync.TryApply(Tasks, persisted))
        {
            await LoadTasksForSelectedProjectAsync();
            return;
        }

        TaskRowListSync.Reorder(Tasks);
        OnPropertyChanged(nameof(IsTaskListEmpty));
    }

    /// <summary>
    /// 保存到当前下拉项目并留在小窗（D4/D5）：输入清空、列表立即出现新任务，可连续录入。
    /// 空白标题静默忽略。
    /// </summary>
    /// <remarks>与主窗创建栏共用 <see cref="AddTaskViewModel"/>，不另写一套创建不变量。</remarks>
    [RelayCommand]
    private async Task SaveAsync()
        => await new AddTaskViewModel(_taskRepository, _clock).ExecuteAsync(
            InputText,
            Priority,
            dueDate: null,
            SelectedProject?.Id,
            ResetInput,
            LoadTasksForSelectedProjectAsync,
            this);

    [RelayCommand]
    private void Cancel()
    {
        ResetInput();
        RequestClose?.Invoke();
    }

    private void ResetInput()
    {
        InputText = string.Empty;
        Priority = TaskPriority.Medium;
    }
}
