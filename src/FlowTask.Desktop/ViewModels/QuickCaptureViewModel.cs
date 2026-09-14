using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FlowTask.Core.Enums;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Messages;
using FlowTask.Core.Models;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 随手记浮窗视图模型：承载极速捕捉输入与优先级选择。
/// </summary>
public partial class QuickCaptureViewModel : ViewModelBase
{
    private readonly ITaskRepository _repository;
    private readonly IClock _clock;

    [ObservableProperty]
    private string _inputText = string.Empty;

    /// <summary>
    /// 捕捉项优先级，默认中优先级。
    /// </summary>
    /// <remarks>
    /// 原实现以三个独立布尔量表示单选状态，三者可能同时为真或同时为假，
    /// 属于可被单一枚举完整表达的冗余状态（Article 10）。
    /// </remarks>
    [ObservableProperty]
    private TaskPriority _priority = TaskPriority.Medium;

    /// <summary>请求关闭浮窗。由视图层订阅，ViewModel 不持有窗口引用。</summary>
    public event Action? RequestClose;

    /// <summary>
    /// 构造随手记视图模型。
    /// </summary>
    /// <param name="repository">任务仓储。</param>
    /// <param name="clock">时间提供者，用于显式赋值创建时刻（Article 9）。</param>
    public QuickCaptureViewModel(ITaskRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>
    /// 保存捕捉项并关闭浮窗。空白输入静默忽略，避免误触产生空任务。
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        // 与主窗口创建路径共用 TaskTitle 规则（Article 6）
        if (!TaskTitle.IsValid(InputText))
        {
            return;
        }

        // 与主窗口共用同一创建入口，保证两条捕捉路径的不变量一致（Article 6）
        var task = TaskItemFactory.Create(_clock, InputText, Priority);

        await _repository.SaveTaskAsync(task);

        // 弱引用广播通知主窗口刷新，双方无强引用关联 (rule-code-standards §2.1)
        WeakReferenceMessenger.Default.Send(new TaskSavedMessage(task));

        ResetInput();
        RequestClose?.Invoke();
    }

    /// <summary>
    /// 放弃当前输入并关闭浮窗。
    /// </summary>
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
