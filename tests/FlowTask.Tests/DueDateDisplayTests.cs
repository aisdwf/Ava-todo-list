using FlowTask.Core.Models;
using FlowTask.Desktop.Converters;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 到期日文案必须可注入「今天」，跨午夜后才能换相对描述（扫描 L3）。
/// </summary>
public class DueDateDisplayTests : IDisposable
{
    public void Dispose() => DueDateDisplay.Today = static () => DateTime.Today;

    [Fact]
    public void DelayUntilNextMidnight_IsTimeUntilTomorrow()
    {
        var now = new DateTime(2026, 3, 10, 22, 30, 0);
        Assert.Equal(TimeSpan.FromHours(1.5), MidnightRefresh.DelayUntilNextMidnight(now));
    }

    [Fact]
    public void DueDateText_UsesInjectedToday()
    {
        DueDateDisplay.Today = static () => new DateTime(2026, 3, 11);
        var text = DueDateTextConverter.Instance.Convert(
            new DateTime(2026, 3, 10), typeof(string), null, null!);

        Assert.Equal("昨天到期", text);
    }

    [Fact]
    public void OverdueAndToday_UseInjectedToday()
    {
        DueDateDisplay.Today = static () => new DateTime(2026, 3, 11);
        var due = new DateTime(2026, 3, 10);
        Assert.True((bool)DueDateOverdueConverter.Instance.Convert(due, typeof(bool), null, null!)!);
        Assert.False((bool)DueDateTodayConverter.Instance.Convert(due, typeof(bool), null, null!)!);
        Assert.True((bool)DueDateTodayConverter.Instance.Convert(
            new DateTime(2026, 3, 11), typeof(bool), null, null!)!);
    }
}
