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
/// 快捷小窗：热键显隐 + <c>@项目</c> 解析与补全（spec-quick-window-hotkey-capture）。
/// </summary>
public partial class QuickCaptureViewModel : ViewModelBase
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

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private TaskPriority _priority = TaskPriority.Medium;

    [ObservableProperty]
    private bool _isCompletionOpen;

    [ObservableProperty]
    private int _selectedCompletionIndex;

    /// <summary>当前补全候选（项目名，不含 <c>@</c>）。</summary>
    public ObservableCollection<string> CompletionItems { get; } = [];

    /// <summary>
    /// 项目下拉候选（含系统 Default），驱动小窗单项目列表切换（D1：下拉）。
    /// </summary>
    public ObservableCollection<ProjectItemViewModel> Projects { get; } = [];

    /// <summary>当前选中项目；驱动任务列表查询与「记忆上次项目」持久化。</summary>
    [ObservableProperty]
    private ProjectItemViewModel? _selectedProject;

    /// <summary>
    /// 当前选中项目下的任务（单项目列表，spec-quick-window-single-project-list）。
    /// </summary>
    /// <remarks>直接复用主窗 <see cref="TaskRowViewModel"/>，勾选走与主窗同一套完成命令，
    /// 避免重复实现「完成 ≠ 归档」的语义（spec-task-complete-before-archive）。</remarks>
    public ObservableCollection<TaskRowViewModel> Tasks { get; } = [];

    /// <summary>列表是否为空，驱动空状态提示显隐。</summary>
    public bool IsTaskListEmpty => Tasks.Count == 0;

    /// <summary>请求关闭浮窗。由视图层订阅，ViewModel 不持有窗口引用。</summary>
    public event Action? RequestClose;

    /// <summary>请求将输入框光标移到指定位置（补全接受后跟到词尾）。</summary>
    public event Action<int>? RequestSetCaret;

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
    }

    /// <summary>
    /// 打开浮窗前刷新项目缓存，恢复上次选中项目并载入其任务列表。
    /// </summary>
    public async Task PrepareAsync()
    {
        await _projectRepository.EnsureDefaultProjectAsync(_clock.UtcNow);
        _projects = await _projectRepository.GetActiveProjectsAsync();
        RefreshCompletion();

        await RefreshProjectChoicesAsync();
    }

    partial void OnInputTextChanged(string value) => RefreshCompletion();

    /// <summary>
    /// 重建项目下拉候选并恢复上次选中项，同时载入对应任务列表。
    /// </summary>
    private async Task RefreshProjectChoicesAsync()
    {
        var lastProjectId = await _settingsRepository.GetAsync(LastProjectSettingsKey);

        Projects.Clear();
        foreach (var project in _projects)
        {
            Projects.Add(new ProjectItemViewModel(project, 0));
        }

        var restored = Projects.FirstOrDefault(p => p.Id == lastProjectId)
                       ?? Projects.FirstOrDefault(p => p.Id == DefaultProject.Id);

        _suppressProjectSelectionReload = true;
        try
        {
            SelectedProject = restored;
        }
        finally
        {
            _suppressProjectSelectionReload = false;
        }

        await LoadTasksForSelectedProjectAsync();
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

        _ = ChangeSelectedProjectCommand.ExecuteAsync(value);
    }

    [RelayCommand]
    private async Task ChangeSelectedProjectAsync(ProjectItemViewModel? value)
    {
        await _settingsRepository.SetAsync(LastProjectSettingsKey, value?.Id ?? DefaultProject.Id);
        await LoadTasksForSelectedProjectAsync();
    }

    /// <summary>
    /// 按当前选中项目载入任务，排序按 D2：未完成在上，已完成未归档置底。
    /// </summary>
    private async Task LoadTasksForSelectedProjectAsync()
    {
        var projectId = SelectedProject?.Id ?? DefaultProject.Id;
        var items = await _taskRepository.GetTasksByProjectAsync(projectId);

        var project = _projects.FirstOrDefault(p => p.Id == projectId);

        // D2：未完成在上（与主窗项目视图同序：优先级降序、到期日升序），
        // 已完成未归档置底（按完成时刻降序，最近完成的在前）
        var pending = items
            .Where(t => !t.IsCompleted)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue);
        var completed = items
            .Where(t => t.IsCompleted)
            .OrderByDescending(t => t.CompletedAt);
        var ordered = pending.Concat(completed);

        Tasks.Clear();
        foreach (var item in ordered)
        {
            Tasks.Add(new TaskRowViewModel(item, project));
        }

        OnPropertyChanged(nameof(IsTaskListEmpty));
    }

    /// <summary>
    /// 切换任务完成状态。与主窗共用同一套「完成 ≠ 归档」语义
    /// （spec-task-complete-before-archive）：勾选后任务仍留在列表，不立即消失。
    /// </summary>
    [RelayCommand]
    private async Task ToggleTaskCompleteAsync(TaskItem? item)
        => await new ToggleCompleteTaskViewModel(_taskRepository, _clock)
            .ExecuteAsync(item, LoadTasksForSelectedProjectAsync);

    /// <summary>
    /// 保存捕捉项并关闭浮窗。空白标题静默忽略。
    /// 未知 <c>@</c> 在保存时创建实体（R-1.8）。
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var parsed = CaptureInputParser.Parse(
            InputText,
            _projects.Select(p => p.Name));

        if (!TaskTitle.IsValid(parsed.Title))
        {
            return;
        }

        var projectId = await ResolveOrCreateProjectAsync(parsed.ProjectName);

        var task = TaskItemFactory.Create(_clock, parsed.Title, Priority, projectId);
        await _taskRepository.SaveTaskAsync(task);

        WeakReferenceMessenger.Default.Send(new TaskSavedMessage(task));

        // 新任务落在当前小窗选中的项目时立即刷新列表，不需要关闭再重开才能看到（Q7）
        if (projectId == (SelectedProject?.Id ?? DefaultProject.Id))
        {
            await LoadTasksForSelectedProjectAsync();
        }

        ResetInput();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        ResetInput();
        RequestClose?.Invoke();
    }

    /// <summary>接受当前补全项，替换正在输入的 <c>@</c> token。</summary>
    [RelayCommand]
    private void AcceptCompletion()
    {
        if (!IsCompletionOpen || CompletionItems.Count == 0)
        {
            return;
        }

        var index = Math.Clamp(SelectedCompletionIndex, 0, CompletionItems.Count - 1);
        AcceptCompletionChoice(CompletionItems[index]);
    }

    /// <summary>接受指定补全项（鼠标点选）。</summary>
    [RelayCommand]
    private void AcceptCompletionChoice(string? choice)
    {
        if (string.IsNullOrEmpty(choice))
        {
            return;
        }

        if (!CaptureInputParser.TryGetCompletionToken(
                InputText, InputText.Length, out _, out var tokenStart))
        {
            return;
        }

        var prefix = InputText[..tokenStart];
        InputText = $"{prefix}@{choice} ";
        IsCompletionOpen = false;
        CompletionItems.Clear();
        RequestSetCaret?.Invoke(InputText.Length);
    }

    [RelayCommand]
    private void SelectNextCompletion()
    {
        if (CompletionItems.Count == 0)
        {
            return;
        }

        SelectedCompletionIndex = (SelectedCompletionIndex + 1) % CompletionItems.Count;
    }

    [RelayCommand]
    private void SelectPreviousCompletion()
    {
        if (CompletionItems.Count == 0)
        {
            return;
        }

        SelectedCompletionIndex =
            (SelectedCompletionIndex - 1 + CompletionItems.Count) % CompletionItems.Count;
    }

    private async Task<string> ResolveOrCreateProjectAsync(string? projectName)
    {
        if (projectName is null)
        {
            return DefaultProject.Id;
        }

        var match = _projects.FirstOrDefault(p =>
            string.Equals(
                ProjectName.Normalize(p.Name),
                ProjectName.Normalize(projectName),
                StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match.Id;
        }

        if (!ProjectName.IsValid(projectName))
        {
            return DefaultProject.Id;
        }

        var project = new Project
        {
            Name = ProjectName.Normalize(projectName),
            SortOrder = _projects.Count,
            ColorHex = AppearanceCoordinator.PickPaletteColor(_projects.Count),
            CreatedAt = _clock.UtcNow
        };
        await _projectRepository.SaveProjectAsync(project);
        _projects.Add(project);
        return project.Id;
    }

    private void RefreshCompletion()
    {
        CompletionItems.Clear();
        IsCompletionOpen = false;
        SelectedCompletionIndex = 0;

        if (!CaptureInputParser.TryGetCompletionToken(
                InputText, InputText.Length, out var prefix, out _))
        {
            return;
        }

        foreach (var name in _projects
                     .Select(p => p.Name)
                     .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                     .Take(8))
        {
            CompletionItems.Add(name);
        }

        IsCompletionOpen = CompletionItems.Count > 0;
    }

    private void ResetInput()
    {
        InputText = string.Empty;
        Priority = TaskPriority.Medium;
        CompletionItems.Clear();
        IsCompletionOpen = false;
        SelectedCompletionIndex = 0;
    }
}
