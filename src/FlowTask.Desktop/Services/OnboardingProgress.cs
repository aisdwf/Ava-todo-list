using System.Globalization;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 首次引导完成标记的解析与序列化（spec-onboarding-guide）。
/// </summary>
/// <remarks>
/// 存版本号而非布尔：引导内容改版时提高 <see cref="CurrentVersion"/>，
/// 老用户会再看一次新版引导，不需要迁移旧键。
/// </remarks>
public static class OnboardingProgress
{
    /// <summary>AppSettings 键。缺键视为从未看过。</summary>
    public const string SettingsKey = "Onboarding.CompletedVersion";

    /// <summary>当前引导内容版本。</summary>
    public const int CurrentVersion = 1;

    /// <summary>把存值解析为已完成的版本号；空、坏值、负数一律视为 0（未看过）。</summary>
    public static int Parse(string? raw)
        => int.TryParse(raw?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : 0;

    /// <summary>已完成版本低于当前版本时自动播放。</summary>
    public static bool ShouldAutoStart(int completedVersion) => completedVersion < CurrentVersion;

    /// <summary>把版本号写成存值。</summary>
    public static string ToStorage(int version) => version.ToString(CultureInfo.InvariantCulture);
}
