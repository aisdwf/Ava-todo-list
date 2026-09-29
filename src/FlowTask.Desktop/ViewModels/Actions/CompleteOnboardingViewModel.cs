using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop.ViewModels.Actions;

/// <summary>
/// 记下首次引导已完成（含跳过）当前版本（TR-1；spec-onboarding-guide）。
/// </summary>
public sealed class CompleteOnboardingViewModel
{
    private readonly IAppSettingsRepository _settingsRepository;

    public CompleteOnboardingViewModel(IAppSettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    /// <summary>
    /// 写入当前版本号。写库失败只记日志：引导已经关掉，下次启动再播一次比让界面报错更可接受。
    /// </summary>
    public async Task ExecuteAsync()
    {
        try
        {
            await _settingsRepository.SetAsync(
                OnboardingProgress.SettingsKey,
                OnboardingProgress.ToStorage(OnboardingProgress.CurrentVersion));
        }
        catch (Exception ex)
        {
            AppLog.Write("CompleteOnboarding", ex);
        }
    }
}
