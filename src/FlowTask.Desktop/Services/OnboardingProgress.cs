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
    /// <remarks>
    /// 2：spec-due-date-picker 把添加栏改为「标题 | 到期入口 | P1–P3」单行，第 2 步「到期日与优先级」的讲解与挖空位置随之改变（BR-1 第 3 条）。
    /// 3：spec-inline-task-edit 取消点标题展开编辑面板，改为双击标题改名、点 P 标签弹框改优先级，第 3 步讲解随之改变。
    /// </remarks>
    public const int CurrentVersion = 3;

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
