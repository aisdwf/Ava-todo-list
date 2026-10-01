using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>一次换键请求的结局。</summary>
public enum HotkeyChangeOutcome
{
    /// <summary>已注册并保存。</summary>
    Applied,

    /// <summary>已注册生效，但写库失败；本次运行有效，重启后回到旧值。</summary>
    AppliedNotSaved,

    /// <summary>组合不合规或属于保留组合，未尝试注册。</summary>
    Rejected,

    /// <summary>已被其他程序占用，旧组合保持不变。</summary>
    Occupied,

    /// <summary>注册失败（非占用），旧组合保持不变。</summary>
    Failed,

    /// <summary>当前平台不支持自定义。</summary>
    Unsupported
}

/// <summary>
/// 修改快捷小窗快捷键（TR-1；spec-quick-window-custom-hotkey W3）。
/// </summary>
/// <remarks>
/// 顺序固定为「校验 → 注册 → 成功才写库」：先写库再注册，注册失败时库里就留下一个
/// 下次启动必然失败的组合；注册器保证失败时旧组合仍生效，所以这里不需要回滚。
/// </remarks>
public sealed class ChangeQuickWindowHotkeyViewModel
{
    private readonly IQuickWindowHotkeyRegistrar _registrar;
    private readonly IAppSettingsRepository _settingsRepository;

    public ChangeQuickWindowHotkeyViewModel(
        IQuickWindowHotkeyRegistrar registrar,
        IAppSettingsRepository settingsRepository)
    {
        _registrar = registrar;
        _settingsRepository = settingsRepository;
    }

    /// <summary>尝试换成 <paramref name="candidate"/>。</summary>
    public async Task<HotkeyChangeOutcome> ExecuteAsync(QuickWindowHotkey candidate)
    {
        if (!_registrar.SupportsCustomHotkey)
        {
            return HotkeyChangeOutcome.Unsupported;
        }

        if (candidate.Validate() != HotkeyValidation.Valid)
        {
            return HotkeyChangeOutcome.Rejected;
        }

        var outcome = await _registrar.TryApplyAsync(candidate);
        switch (outcome)
        {
            case HotkeyRegistrationOutcome.Registered:
                break;
            case HotkeyRegistrationOutcome.Occupied:
                return HotkeyChangeOutcome.Occupied;
            case HotkeyRegistrationOutcome.Unsupported:
                return HotkeyChangeOutcome.Unsupported;
            default:
                return HotkeyChangeOutcome.Failed;
        }

        try
        {
            await _settingsRepository.SetAsync(QuickWindowHotkey.SettingsKey, candidate.ToStorage());
            return HotkeyChangeOutcome.Applied;
        }
        catch (Exception ex)
        {
            AppLog.Write("ChangeQuickWindowHotkey persist", ex);
            return HotkeyChangeOutcome.AppliedNotSaved;
        }
    }
}
