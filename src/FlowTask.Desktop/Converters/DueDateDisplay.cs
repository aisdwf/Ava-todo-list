namespace FlowTask.Desktop.Converters;

/// <summary>
/// 到期日展示用的「今天」。默认真读本地日历；测试可替换，午夜刷新后重建列表会再求值。
/// </summary>
public static class DueDateDisplay
{
    public static Func<DateTime> Today { get; set; } = static () => DateTime.Today;
}

/// <summary>
/// 距下一个本地午夜的等待间隔，供托盘常驻后刷新「今天到期」文案（扫描 L3）。
/// </summary>
public static class MidnightRefresh
{
    public static TimeSpan DelayUntilNextMidnight(DateTime nowLocal)
    {
        var next = nowLocal.Date.AddDays(1);
        var delay = next - nowLocal;
        return delay <= TimeSpan.Zero ? TimeSpan.FromDays(1) : delay;
    }
}
