namespace FlowTask.Core.Models;

/// <summary>
/// 「启用默认到期」时相对今天的偏移天数校验真源。
/// </summary>
public static class DueDateOffset
{
    public const int MinDays = 1;
    public const int MaxDays = 30;
    public const int DefaultDays = 1;
    public const string SettingsKey = "default_due_offset_days";

    public static string? Validate(int days)
    {
        if (days < MinDays || days > MaxDays)
        {
            return $"默认到期偏移须在 {MinDays}–{MaxDays} 天之间。";
        }

        return null;
    }

    public static bool IsValid(int days) => Validate(days) is null;

    public static int ClampOrDefault(int days) =>
        IsValid(days) ? days : DefaultDays;
}
