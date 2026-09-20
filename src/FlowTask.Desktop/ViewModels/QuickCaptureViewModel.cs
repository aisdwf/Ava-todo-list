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
/// 随手记浮窗：热键显隐 + <c>@项目</c>/<c>#标签</c> 解析与补全（spec-quick-window-hotkey-capture）。
/// </summary>
public partial class QuickCaptureViewModel : ViewModelBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IClock _clock;

    private List<Project> _projects = [];
    private List<Tag> _tags = [];

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private TaskPriority _priority = TaskPriority.Medium;

    [ObservableProperty]
    private bool _isCompletionOpen;

    [ObservableProperty]
    private int _selectedCompletionIndex;

    /// <summary>当前补全候选（项目或标签名，不含 sigil）。</summary>
    public ObservableCollection<string> CompletionItems { get; } = [];

    /// <summary>请求关闭浮窗。由视图层订阅，ViewModel 不持有窗口引用。</summary>
    public event Action? RequestClose;

    /// <summary>请求将输入框光标移到指定位置（补全接受后跟到词尾）。</summary>
    public event Action<int>? RequestSetCaret;

    /// <summary>
    /// 构造随手记视图模型。
    /// </summary>
    public QuickCaptureViewModel(
        ITaskRepository taskRepository,
        IProjectRepository projectRepository,
        ITagRepository tagRepository,
        IClock clock)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _tagRepository = tagRepository;
        _clock = clock;
    }

    /// <summary>打开浮窗前刷新项目/标签缓存，供解析与补全使用。</summary>
    public async Task PrepareAsync()
    {
        await _projectRepository.EnsureDefaultProjectAsync(_clock.UtcNow);
        _projects = await _projectRepository.GetActiveProjectsAsync();
        _tags = await _tagRepository.GetAllAsync();
        RefreshCompletion();
    }

    partial void OnInputTextChanged(string value) => RefreshCompletion();

    /// <summary>
    /// 保存捕捉项并关闭浮窗。空白标题静默忽略。
    /// 未知 <c>@</c>/<c>#</c> 在保存时创建实体（R-1.8）。
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var parsed = CaptureInputParser.Parse(
            InputText,
            _projects.Select(p => p.Name),
            _tags.Select(t => t.Name));

        if (!TaskTitle.IsValid(parsed.Title))
        {
            return;
        }

        var projectId = await ResolveOrCreateProjectAsync(parsed.ProjectName);
        var tagIds = await ResolveOrCreateTagsAsync(parsed.TagNames);

        var task = TaskItemFactory.Create(_clock, parsed.Title, Priority, projectId);
        await _taskRepository.SaveTaskAsync(task);
        if (tagIds.Count > 0)
        {
            await _tagRepository.ReplaceTaskTagsAsync(task.Id, tagIds);
        }

        WeakReferenceMessenger.Default.Send(new TaskSavedMessage(task));

        ResetInput();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        ResetInput();
        RequestClose?.Invoke();
    }

    /// <summary>接受当前补全项，替换正在输入的 <c>@</c>/<c>#</c> token。</summary>
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
                InputText, InputText.Length, out var sigil, out _, out var tokenStart))
        {
            return;
        }

        var prefix = InputText[..tokenStart];
        InputText = $"{prefix}{sigil}{choice} ";
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

    private async Task<List<string>> ResolveOrCreateTagsAsync(IReadOnlyList<string> tagNames)
    {
        var tagIds = new List<string>();
        foreach (var tagName in tagNames)
        {
            var tag = _tags.FirstOrDefault(t =>
                string.Equals(
                    TagName.Normalize(t.Name),
                    TagName.Normalize(tagName),
                    StringComparison.OrdinalIgnoreCase));
            if (tag is null)
            {
                if (!TagName.IsValid(tagName))
                {
                    continue;
                }

                tag = new Tag
                {
                    Name = TagName.Normalize(tagName),
                    ColorHex = AppearanceCoordinator.PickPaletteColor(_tags.Count),
                    SortOrder = _tags.Count,
                    CreatedAt = _clock.UtcNow
                };
                await _tagRepository.SaveAsync(tag);
                _tags.Add(tag);
            }

            tagIds.Add(tag.Id);
        }

        return tagIds;
    }

    private void RefreshCompletion()
    {
        CompletionItems.Clear();
        IsCompletionOpen = false;
        SelectedCompletionIndex = 0;

        if (!CaptureInputParser.TryGetCompletionToken(
                InputText, InputText.Length, out var sigil, out var prefix, out _))
        {
            return;
        }

        IEnumerable<string> source = sigil == '@'
            ? _projects.Select(p => p.Name)
            : _tags.Select(t => t.Name);

        foreach (var name in source
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
