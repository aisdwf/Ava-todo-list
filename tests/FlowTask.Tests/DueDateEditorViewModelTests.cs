using FlowTask.Desktop.ViewModels;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 到期日选择器编辑器：预设 + 日历，预览 / 提交分离（spec-due-date-picker）。
/// </summary>
public class DueDateEditorViewModelTests
{
    private readonly FakeClock _clock = new(
        new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 3, 10));

    private DueDateEditorViewModel Create(int offset = 1, DateTime? initial = null)
    {
        var vm = new DueDateEditorViewModel(_clock, () => offset);
        vm.Load(initial);
        return vm;
    }

    [Fact]
    public void Load_InitializesProperties()
    {
        var testDate = new DateTime(2026, 3, 15);
        var vm = Create(initial: testDate);

        Assert.Equal(testDate, vm.SelectedDate);
        Assert.Equal(testDate, vm.CalendarDate);
        Assert.Equal(testDate, vm.CalendarDisplayDate);
    }

    [Fact]
    public void Load_WithNull_ShowsCurrentMonth()
    {
        var vm = Create();

        Assert.Null(vm.SelectedDate);
        Assert.Null(vm.CalendarDate);
        Assert.Equal(_clock.Today, vm.CalendarDisplayDate);
    }

    [Fact]
    public void ApplyDefaultDue_CommitsTodayPlusOffset()
    {
        var vm = Create(offset: 3);
        DateTime? committed = null;
        var fired = false;
        vm.Committed += d => { fired = true; committed = d; };

        vm.ApplyDefaultDueCommand.Execute(null);

        var expected = _clock.Today.AddDays(3);
        Assert.True(fired);
        Assert.Equal(expected, committed);
        Assert.Equal(expected, vm.SelectedDate);
        Assert.Equal("默认 +3 天", vm.DefaultPresetLabel);
    }

    [Fact]
    public void ClearDue_CommitsNull()
    {
        var vm = Create(initial: new DateTime(2026, 3, 20));
        var fired = false;
        DateTime? committed = new DateTime(2000, 1, 1);
        vm.Committed += d => { fired = true; committed = d; };

        vm.ClearDueCommand.Execute(null);

        Assert.True(fired);
        Assert.Null(committed);
        Assert.Null(vm.SelectedDate);
        Assert.Null(vm.CalendarDate);
    }

    /// <summary>
    /// 日历高亮被方向键移动：只改预览，不提交。
    /// </summary>
    [Fact]
    public void CalendarMove_DoesNotCommit()
    {
        var original = new DateTime(2026, 3, 20);
        var vm = Create(initial: original);
        var fired = false;
        vm.Committed += _ => fired = true;

        vm.CalendarDate = new DateTime(2026, 4, 20);

        Assert.Equal(original, vm.SelectedDate);
        Assert.False(fired);
    }

    /// <summary>
    /// 日历上按 Enter：提交当前高亮日。
    /// </summary>
    [Fact]
    public void CommitPreview_CommitsHighlightedDay()
    {
        var vm = Create();
        DateTime? committed = null;
        vm.Committed += d => committed = d;

        vm.CalendarDate = new DateTime(2026, 3, 18);
        vm.CommitPreviewCommand.Execute(null);

        Assert.Equal(new DateTime(2026, 3, 18), committed);
        Assert.Equal(new DateTime(2026, 3, 18), vm.SelectedDate);
    }

    /// <summary>
    /// 未高亮任何日期时按 Enter 不提交：否则会把「未设置」当成「清除」静默写回。
    /// </summary>
    [Fact]
    public void CommitPreview_WithoutHighlight_DoesNothing()
    {
        var vm = Create();
        var fired = false;
        vm.Committed += _ => fired = true;

        vm.CommitPreviewCommand.Execute(null);

        Assert.False(fired);
    }

    [Fact]
    public void CommitDate_CommitsPickedDay()
    {
        var vm = Create();
        DateTime? committed = null;
        vm.Committed += d => committed = d;

        vm.CommitDate(new DateTime(2026, 4, 20));

        Assert.Equal(new DateTime(2026, 4, 20), committed);
        Assert.Equal(new DateTime(2026, 4, 20), vm.TakeValue());
        Assert.Equal(new DateTime(2026, 4, 20), vm.CalendarDisplayDate);
    }

    [Fact]
    public void Load_ResetsPreviousSessionPreview()
    {
        var vm = Create(initial: new DateTime(2026, 3, 20));
        vm.CalendarDate = new DateTime(2026, 5, 1);

        vm.Load(null);

        Assert.Null(vm.CalendarDate);
        Assert.Equal(_clock.Today, vm.CalendarDisplayDate);
    }
}
