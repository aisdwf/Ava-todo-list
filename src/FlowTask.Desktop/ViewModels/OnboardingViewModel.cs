using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;
using FlowTask.Desktop.ViewModels.Actions;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 首次聚光灯引导的步骤状态（spec-onboarding-guide）。
/// </summary>
/// <remarks>
/// 从 <see cref="MainViewModel"/> 拆出（TR-1）：后者只持有本实例并在
/// <see cref="IsActive"/> 变化时刷新遮罩阻断。挖空定位归视图层，这里不引用控件。
/// </remarks>
public partial class OnboardingViewModel : ViewModelBase
{
    private readonly IAppSettingsRepository _settingsRepository;

    /// <summary>引导步骤，来自 <see cref="GuideCatalog.OnboardingSteps"/>。</summary>
    public IReadOnlyList<GuideTopic> Steps { get; } = GuideCatalog.OnboardingSteps;

    /// <summary>气泡底部的步骤点，与 <see cref="Steps"/> 一一对应。</summary>
    public IReadOnlyList<OnboardingStepDot> Dots { get; }

    /// <summary>当前步骤下标。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentStep))]
    [NotifyPropertyChangedFor(nameof(StepCounterText))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(NextText))]
    private int _stepIndex;

    /// <summary>遮罩是否显示。</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>读库后判定需要自动播放；开始播放后清零，避免重复触发。</summary>
    public bool ShouldAutoStart { get; private set; }

    /// <summary>最近一次完成标记写入，供测试等待落盘。</summary>
    public Task CompletionPersistTask { get; private set; } = Task.CompletedTask;

    /// <summary>当前步骤。</summary>
    public GuideTopic CurrentStep => Steps[StepIndex];

    /// <summary>气泡微标：「STEP 1 / 6」。</summary>
    public string StepCounterText => $"STEP {StepIndex + 1} / {Steps.Count}";

    /// <summary>是否显示「上一步」。</summary>
    public bool CanGoBack => StepIndex > 0;

    /// <summary>是否最后一步。</summary>
    public bool IsLastStep => StepIndex == Steps.Count - 1;

    /// <summary>主按钮文案：最后一步改为「开始使用」。</summary>
    public string NextText => IsLastStep ? "开始使用" : "下一步";

    public OnboardingViewModel(IAppSettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
        Dots = Steps.Select((_, index) => new OnboardingStepDot { IsCurrent = index == 0 }).ToArray();
    }

    /// <summary>读取完成标记，决定 <see cref="ShouldAutoStart"/>。</summary>
    public async Task LoadAsync()
    {
        await _settingsRepository.InitializeAsync();
        var raw = await _settingsRepository.GetAsync(OnboardingProgress.SettingsKey);
        ShouldAutoStart = OnboardingProgress.ShouldAutoStart(OnboardingProgress.Parse(raw));
    }

    /// <summary>从第一步开始播放。首次自动播放与指南页「重新播放」共用。</summary>
    public void Start()
    {
        ShouldAutoStart = false;
        StepIndex = 0;
        IsActive = true;
    }

    /// <summary>下一步；最后一步即完成。</summary>
    [RelayCommand]
    private void Next()
    {
        if (!IsActive)
        {
            return;
        }

        if (IsLastStep)
        {
            Finish();
            return;
        }

        StepIndex++;
    }

    /// <summary>上一步；第一步时不动。</summary>
    [RelayCommand]
    private void Previous()
    {
        if (IsActive && StepIndex > 0)
        {
            StepIndex--;
        }
    }

    /// <summary>跳过：与完成同样记为已看过（design-interaction-principles §2.2 例外条款）。</summary>
    [RelayCommand]
    private void Skip() => Finish();

    private void Finish()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        CompletionPersistTask = LoggedTasks.Observe(
            new CompleteOnboardingViewModel(_settingsRepository).ExecuteAsync(),
            "CompleteOnboarding");
    }

    partial void OnStepIndexChanged(int value)
    {
        for (var i = 0; i < Dots.Count; i++)
        {
            Dots[i].IsCurrent = i == value;
        }
    }
}

/// <summary>引导气泡底部的一个步骤点。</summary>
public partial class OnboardingStepDot : ObservableObject
{
    /// <summary>是否当前步骤。</summary>
    [ObservableProperty]
    private bool _isCurrent;
}
