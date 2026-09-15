using FlowTask.Desktop.ViewModels;
using Xunit;

namespace FlowTask.Tests;

public class DueDateEditorViewModelTests : IDisposable
{
    private readonly FakeClock _clock = new(
        new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 3, 10));

    public void Dispose() { }

    [Fact]
    public void Load_InitializesProperties()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        var testDate = new DateTime(2026, 3, 15);

        vm.Load(testDate);

        Assert.Equal(testDate, vm.SelectedDate);
        Assert.Equal(testDate, vm.CalendarDate);
        Assert.Empty(vm.DigitText);
        Assert.False(vm.HasParseError);
    }

    [Fact]
    public void Load_WithNull_InitializesToNull()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);

        vm.Load(null);

        Assert.Null(vm.SelectedDate);
        Assert.Null(vm.CalendarDate);
    }

    [Fact]
    public void EnableDefaultDueCommand_SetsDateToTodayPlusOffset()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 3);
        vm.Load(null);

        vm.EnableDefaultDueCommand.Execute(null);

        var expected = _clock.Today.AddDays(3);
        Assert.Equal(expected, vm.SelectedDate);
        Assert.Equal(expected, vm.CalendarDate);
    }

    [Fact]
    public void ClearDueCommand_SetsAllToNull()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        vm.Load(new DateTime(2026, 3, 20));

        vm.ClearDueCommand.Execute(null);

        Assert.Null(vm.SelectedDate);
        Assert.Null(vm.CalendarDate);
        Assert.Empty(vm.DigitText);
        Assert.False(vm.HasParseError);
    }

    [Fact]
    public void OnDigitTextChanged_WithValidSingleDigit_UpdatesDate()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        vm.Load(null);

        vm.UpdateDigitInput("15");

        Assert.NotNull(vm.SelectedDate);
        Assert.Equal(2026, vm.SelectedDate.Value.Year);
        Assert.Equal(3, vm.SelectedDate.Value.Month);
        Assert.Equal(15, vm.SelectedDate.Value.Day);
        Assert.False(vm.HasParseError);
    }

    [Fact]
    public void OnDigitTextChanged_WithValidMonthDay_UpdatesDate()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        vm.Load(null);

        vm.UpdateDigitInput("0510");

        Assert.NotNull(vm.SelectedDate);
        Assert.Equal(2026, vm.SelectedDate.Value.Year);
        Assert.Equal(5, vm.SelectedDate.Value.Month);
        Assert.Equal(10, vm.SelectedDate.Value.Day);
        Assert.False(vm.HasParseError);
    }

    [Fact]
    public void OnDigitTextChanged_WithValidFullYear_UpdatesDate()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        vm.Load(null);

        vm.UpdateDigitInput("20250615");

        Assert.NotNull(vm.SelectedDate);
        Assert.Equal(2025, vm.SelectedDate.Value.Year);
        Assert.Equal(6, vm.SelectedDate.Value.Month);
        Assert.Equal(15, vm.SelectedDate.Value.Day);
        Assert.False(vm.HasParseError);
    }

    [Fact]
    public void OnDigitTextChanged_WithValidIsoString_UpdatesDate()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        vm.Load(null);

        vm.UpdateDigitInput("2025-06-15");

        Assert.NotNull(vm.SelectedDate);
        Assert.Equal(2025, vm.SelectedDate.Value.Year);
        Assert.Equal(6, vm.SelectedDate.Value.Month);
        Assert.Equal(15, vm.SelectedDate.Value.Day);
        Assert.False(vm.HasParseError);
    }

    [Fact]
    public void OnDigitTextChanged_WithInvalid_PreservesDateAndSetsError()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        var originalDate = new DateTime(2026, 3, 15);
        vm.Load(originalDate);

        vm.UpdateDigitInput("abc");

        // 原值保留
        Assert.Equal(originalDate, vm.SelectedDate);
        // 错误标志置位
        Assert.True(vm.HasParseError);
        Assert.NotEmpty(vm.ParseErrorMessage);
    }

    [Fact]
    public void OnDigitTextChanged_WithInvalidDay_PreservesDateAndSetsError()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        var originalDate = new DateTime(2026, 3, 15);
        vm.Load(originalDate);

        vm.UpdateDigitInput("32");  // 3 月没有 32 号

        Assert.Equal(originalDate, vm.SelectedDate);
        Assert.True(vm.HasParseError);
    }

    [Fact]
    public void OnDigitTextChanged_WithEmpty_DoesNotClearDate()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        var originalDate = new DateTime(2026, 3, 15);
        vm.Load(originalDate);

        vm.UpdateDigitInput("");

        // 空输入不主动清除
        Assert.Equal(originalDate, vm.SelectedDate);
        Assert.Empty(vm.DigitText);
        Assert.False(vm.HasParseError);
    }

    [Fact]
    public void OnCalendarDateChanged_UpdatesAllFields()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        vm.Load(null);
        vm.UpdateDigitInput("15");  // 先设置数字输入

        var newDate = new DateTime(2026, 4, 20);
        vm.UpdateCalendarInput(newDate);

        Assert.Equal(newDate, vm.SelectedDate);
        Assert.Equal(newDate, vm.CalendarDate);
        Assert.Empty(vm.DigitText);
        Assert.False(vm.HasParseError);
    }

    [Fact]
    public void TakeValue_ReturnsCurrentDate()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        var testDate = new DateTime(2026, 3, 20);
        vm.Load(testDate);

        var result = vm.TakeValue();

        Assert.Equal(testDate, result);
    }

    [Fact]
    public void TakeValue_WithNull_ReturnsNull()
    {
        var vm = new DueDateEditorViewModel(_clock, () => 1);
        vm.Load(null);

        var result = vm.TakeValue();

        Assert.Null(result);
    }
}
