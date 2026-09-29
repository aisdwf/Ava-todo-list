using FlowTask.Desktop.ViewModels;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 到期日选择器编辑器：三来源同步 + 预览 / 提交分离（spec-due-date-picker）。
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
        Assert.Empty(vm.DigitText);
        Assert.False(vm.HasParseError);
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

    [Theory]
    [InlineData("15", 2026, 3, 15)]
    [InlineData("0510", 2026, 5, 10)]
    [InlineData("20250615", 2025, 6, 15)]
    [InlineData("2025-06-15", 2025, 6, 15)]
    public void DigitInput_PreviewsWithoutCommitting(string input, int y, int m, int d)
    {
        var original = new DateTime(2026, 3, 20);
        var vm = Create(initial: original);
        var fired = false;
        vm.Committed += _ => fired = true;

        vm.DigitText = input;

        var expected = new DateTime(y, m, d);
        Assert.Equal(expected, vm.CalendarDate);
        Assert.Equal(expected, vm.CalendarDisplayDate);
        Assert.Equal(original, vm.SelectedDate);
        Assert.False(fired);
        Assert.False(vm.HasParseError);
    }

    /// <summary>
    /// 逐键输入 "1" → "10" 的中间态不得被提交：此前输 10 会先把 1 日写进任务。
    /// </summary>
    [Fact]
    public void DigitInput_IntermediateKeystrokeIsNotCommitted()
    {
        var vm = Create();
        var commits = 0;
        vm.Committed += _ => commits++;

        vm.DigitText = "1";
        vm.DigitText = "10";

        Assert.Equal(0, commits);
        Assert.Null(vm.SelectedDate);
        Assert.Equal(new DateTime(2026, 3, 10), vm.CalendarDate);
    }

    [Fact]
    public void CommitPreview_CommitsParsedDigits()
    {
        var vm = Create();
        DateTime? committed = null;
        vm.Committed += d => committed = d;

        vm.DigitText = "0310";
        vm.CommitPreviewCommand.Execute(null);

        Assert.Equal(new DateTime(2026, 3, 10), committed);
        Assert.Equal(new DateTime(2026, 3, 10), vm.SelectedDate);
        Assert.Empty(vm.DigitText);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("32")]
    public void InvalidDigits_KeepPreviewShowErrorAndBlockCommit(string input)
    {
        var original = new DateTime(2026, 3, 15);
        var vm = Create(initial: original);
        var fired = false;
        vm.Committed += _ => fired = true;

        vm.DigitText = input;
        vm.CommitPreviewCommand.Execute(null);

        Assert.True(vm.HasParseError);
        Assert.NotEmpty(vm.ParseErrorMessage);
        Assert.Equal(original, vm.CalendarDate);
        Assert.Equal(original, vm.SelectedDate);
        Assert.False(fired);
    }

    [Fact]
    public void EmptyDigits_ClearErrorAndKeepDate()
    {
        var original = new DateTime(2026, 3, 15);
        var vm = Create(initial: original);

        vm.DigitText = "abc";
        vm.DigitText = string.Empty;

        Assert.False(vm.HasParseError);
        Assert.Equal(original, vm.CalendarDate);
        Assert.Equal(original, vm.SelectedDate);
    }

    /// <summary>
    /// 日历高亮被方向键移动：只改预览、清掉数字框旧文本，不提交。
    /// </summary>
    [Fact]
    public void CalendarMove_ClearsDigitsWithoutCommitting()
    {
        var vm = Create();
        var fired = false;
        vm.Committed += _ => fired = true;
        vm.DigitText = "15";

        vm.CalendarDate = new DateTime(2026, 4, 20);

        Assert.Empty(vm.DigitText);
        Assert.Null(vm.SelectedDate);
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
    }

    [Fact]
    public void Load_ResetsPreviousSessionPreview()
    {
        var vm = Create(initial: new DateTime(2026, 3, 20));
        vm.DigitText = "abc";

        vm.Load(null);

        Assert.Null(vm.CalendarDate);
        Assert.Empty(vm.DigitText);
        Assert.False(vm.HasParseError);
    }
}
