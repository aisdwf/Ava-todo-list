using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;
using FlowTask.Desktop.ViewModels.Actions;

namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 快捷小窗快捷键的运行状态与设置 → 通用里的录制行（spec-quick-window-custom-hotkey）。
/// </summary>
/// <remarks>
/// 从 <see cref="MainViewModel"/> 拆出（TR-1），做法同 <see cref="OnboardingViewModel"/>：
/// 主视图模型只持有本实例，并在占用弹窗显隐时刷新遮罩阻断。
/// <para>
/// 三种运行状态互斥：系统级生效（<see cref="IsSystemActive"/>）、本次运行停用（<see cref="IsSuspended"/>）、
/// 都不是时由窗内 KeyDown 回退（非 Windows 且固定注册失败）。停用只限本次运行，不改写已保存组合
/// （owner 裁决「临时对本项目禁用快捷键」）。
/// </para>
/// </remarks>
public partial class QuickWindowHotkeyViewModel : ViewModelBase
{
    private const string RecordingHint =
        "请按下新的组合键，Esc 取消。按下没有反应，说明该组合已被其他程序占用。";

    private readonly IQuickWindowHotkeyRegistrar _registrar;
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly bool _isMacOS;
    private bool _conflictPromptShown;

    /// <summary>当前组合：生效中、或停用前尝试注册的那个。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    [NotifyPropertyChangedFor(nameof(CurrentText))]
    private QuickWindowHotkey _hotkey = QuickWindowHotkey.Default;

    /// <summary>进程级热键已注册生效。为 true 时窗内不再重复监听。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    [NotifyPropertyChangedFor(nameof(CurrentText))]
    private bool _isSystemActive;

    /// <summary>启动时已保存组合注册失败，本次运行停用快捷键（窗内也不响应）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    [NotifyPropertyChangedFor(nameof(CurrentText))]
    private bool _isSuspended;

    /// <summary>设置行处于录制状态，窗口 KeyDown 交给 <see cref="RecordAsync"/>。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentText))]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    private bool _isRecording;

    /// <summary>注册请求进行中，防止连按造成并发换键。</summary>
    [ObservableProperty]
    private bool _isApplying;

    /// <summary>设置行下方的说明或结果。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusText = string.Empty;

    /// <summary><see cref="StatusText"/> 是否为错误。</summary>
    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>启动占用弹窗是否可见。</summary>
    [ObservableProperty]
    private bool _isConflictPromptOpen;

    /// <summary>启动占用弹窗正文。</summary>
    [ObservableProperty]
    private string _conflictPromptText = string.Empty;

    /// <summary>弹窗「去设置」：请求打开设置 → 通用。</summary>
    public event Action? RequestOpenGeneralSettings;

    public QuickWindowHotkeyViewModel(
        IQuickWindowHotkeyRegistrar registrar,
        IAppSettingsRepository settingsRepository,
        bool? isMacOS = null)
    {
        _registrar = registrar;
        _settingsRepository = settingsRepository;
        _isMacOS = isMacOS ?? OperatingSystem.IsMacOS();
    }

    /// <summary>当前平台能否修改组合。macOS 待适配（TODO(macos-custom-hotkey)）。</summary>
    public bool CanCustomize => _registrar.SupportsCustomHotkey;

    /// <summary>不能修改时设置行给出的说明。</summary>
    public string UnsupportedText => _isMacOS
        ? "macOS 暂不支持修改，固定为 ⌥ Space（待适配）。"
        : "当前系统不支持修改快捷键。";

    /// <summary>侧栏键帽文案。</summary>
    public string Label
    {
        get
        {
            if (!CanCustomize)
            {
                return _isMacOS ? QuickWindowHotkey.MacLabel : Hotkey.ToString();
            }

            return IsSuspended ? "已停用" : Hotkey.ToString();
        }
    }

    /// <summary>设置行键帽文案。</summary>
    public string CurrentText => IsRecording ? "请按键…" : Label;

    /// <summary>「修改」按钮文案，录制中变为「取消」。</summary>
    public string RecordButtonText => IsRecording ? "取消" : "修改";

    /// <summary>是否显示 <see cref="StatusText"/>。</summary>
    public bool HasStatus => !string.IsNullOrEmpty(StatusText);

    /// <summary>
    /// 启动时注册：读已保存组合并注册；失败则停用并弹窗（每次启动最多一次，owner 裁决 Q4「启动提示」）。
    /// 不支持自定义的平台注册固定组合，失败时走窗内回退。
    /// </summary>
    public async Task StartAsync()
    {
        if (!_registrar.SupportsCustomHotkey)
        {
            IsSystemActive = await _registrar.TryStartFixedAsync();
            return;
        }

        await _settingsRepository.InitializeAsync();
        var raw = await _settingsRepository.GetAsync(QuickWindowHotkey.SettingsKey);
        QuickWindowHotkey.TryParse(raw, out var saved);
        Hotkey = saved;

        var outcome = await _registrar.TryApplyAsync(saved);
        if (outcome == HotkeyRegistrationOutcome.Registered)
        {
            IsSystemActive = true;
            IsSuspended = false;
            return;
        }

        IsSystemActive = false;
        IsSuspended = true;
        StatusText = outcome == HotkeyRegistrationOutcome.Occupied
            ? $"{saved} 已被其他程序占用，本次运行已停用。换一个组合即可恢复。"
            : $"{saved} 注册失败，本次运行已停用。换一个组合可以重试，详情见日志。";
        IsStatusError = true;
        OpenConflictPrompt(saved, outcome);
    }

    /// <summary>
    /// 窗内 KeyDown 是否应当切换小窗：只在没有系统级热键、也没有停用、也不在录制时回退。
    /// </summary>
    public bool ShouldHandleInWindow(KeyModifiers modifiers, Key key)
        => !IsSystemActive && !IsSuspended && !IsRecording && Hotkey.Matches(modifiers, key);

    /// <summary>
    /// 录制中按下了当前组合：系统级热键先于窗口收到它。当作「没改」结束录制，不切换小窗。
    /// </summary>
    /// <returns>是否已消费这次热键。</returns>
    public bool TryConsumeWhileRecording()
    {
        if (!IsRecording)
        {
            return false;
        }

        IsRecording = false;
        SetStatus($"{Hotkey} 就是当前组合，未修改。", false);
        return true;
    }

    /// <summary>「修改」/「取消」。</summary>
    [RelayCommand]
    private void ToggleRecording()
    {
        if (!CanCustomize || IsApplying)
        {
            return;
        }

        IsRecording = !IsRecording;
        if (IsRecording)
        {
            SetStatus(RecordingHint, false);
        }
        else
        {
            ClearStatus();
        }
    }

    /// <summary>录制中按 Esc 或离开设置页：放弃，不改任何东西。</summary>
    public void CancelRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        IsRecording = false;
        ClearStatus();
    }

    /// <summary>
    /// 录制中收到窗口按键。只按修饰键时继续等待；不合规的组合给出原因并继续录制。
    /// </summary>
    /// <returns>是否消费了这次按键（录制中总是消费，避免落到按钮或输入框）。</returns>
    public async Task<bool> RecordAsync(KeyModifiers modifiers, Key key)
    {
        if (!IsRecording)
        {
            return false;
        }

        if (key == Key.Escape && modifiers == KeyModifiers.None)
        {
            CancelRecording();
            return true;
        }

        if (QuickWindowHotkey.FromKeyEvent(modifiers, key) is not { } candidate)
        {
            return true;
        }

        if (candidate.DescribeRejection() is { } rejection)
        {
            SetStatus($"{rejection} {RecordingHint}", true);
            return true;
        }

        IsRecording = false;
        await ApplyAsync(candidate);
        return true;
    }

    /// <summary>「恢复默认」：换回 Alt+Space，与录制走同一条注册路径（owner 裁决 Q2）。</summary>
    [RelayCommand]
    private async Task RestoreDefaultAsync()
    {
        if (!CanCustomize)
        {
            return;
        }

        IsRecording = false;
        await ApplyAsync(QuickWindowHotkey.Default);
    }

    /// <summary>弹窗「知道了」/ 点遮罩 / Esc。</summary>
    [RelayCommand]
    private void DismissConflictPrompt() => IsConflictPromptOpen = false;

    /// <summary>弹窗「去设置」。</summary>
    [RelayCommand]
    private void OpenSettingsFromPrompt()
    {
        IsConflictPromptOpen = false;
        RequestOpenGeneralSettings?.Invoke();
    }

    private async Task ApplyAsync(QuickWindowHotkey candidate)
    {
        if (IsApplying)
        {
            return;
        }

        if (candidate == Hotkey && IsSystemActive)
        {
            SetStatus($"当前已是 {candidate}。", false);
            return;
        }

        IsApplying = true;
        try
        {
            var outcome = await new ChangeQuickWindowHotkeyViewModel(_registrar, _settingsRepository)
                .ExecuteAsync(candidate);
            var previous = IsSuspended ? "快捷键仍处于停用状态" : $"仍使用 {Hotkey}";
            switch (outcome)
            {
                case HotkeyChangeOutcome.Applied:
                    Adopt(candidate);
                    SetStatus($"已改为 {candidate}。", false);
                    break;
                case HotkeyChangeOutcome.AppliedNotSaved:
                    Adopt(candidate);
                    SetStatus($"已改为 {candidate}，但保存失败，重启后会恢复原组合。", true);
                    break;
                case HotkeyChangeOutcome.Occupied:
                    SetStatus($"{candidate} 已被其他程序占用，{previous}。", true);
                    break;
                case HotkeyChangeOutcome.Rejected:
                    SetStatus(candidate.DescribeRejection() ?? $"{candidate} 不能使用。", true);
                    break;
                case HotkeyChangeOutcome.Unsupported:
                    SetStatus(UnsupportedText, true);
                    break;
                default:
                    SetStatus($"{candidate} 注册失败，{previous}。详情见日志。", true);
                    break;
            }
        }
        finally
        {
            IsApplying = false;
        }
    }

    private void Adopt(QuickWindowHotkey candidate)
    {
        Hotkey = candidate;
        IsSystemActive = true;
        IsSuspended = false;
    }

    private void OpenConflictPrompt(QuickWindowHotkey saved, HotkeyRegistrationOutcome outcome)
    {
        if (_conflictPromptShown)
        {
            return;
        }

        _conflictPromptShown = true;
        ConflictPromptText = outcome == HotkeyRegistrationOutcome.Occupied
            ? $"快捷键 {saved} 已被其他程序占用，本次运行已停用 FlowTask 的快捷小窗快捷键。可以在 设置 → 通用 换一个组合。"
            : $"快捷键 {saved} 注册失败，本次运行已停用 FlowTask 的快捷小窗快捷键。可以在 设置 → 通用 换一个组合重试。";
        IsConflictPromptOpen = true;
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }

    private void ClearStatus()
    {
        // 停用说明是持续状态，取消录制不能把它也清掉
        if (IsSuspended)
        {
            SetStatus($"{Hotkey} 不可用，本次运行已停用。换一个组合即可恢复。", true);
            return;
        }

        SetStatus(string.Empty, false);
    }
}
