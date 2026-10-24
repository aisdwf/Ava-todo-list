using FlowTask.Core.Enums;
using FlowTask.Core.Models;
using FlowTask.Core.Ordering;
using Xunit;

namespace FlowTask.Tests;

public class TaskListOrderTests
{
    private readonly FakeClock _clock = new(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));

    private TaskItem Item(
        string title,
        TaskPriority priority = TaskPriority.Medium,
        DateTime? dueDate = null,
        bool completed = false,
        DateTime? createdAt = null,
        DateTime? completedAt = null)
    {
        var item = TaskItemFactory.Create(_clock, title, priority, dueDate: dueDate);
        if (createdAt is not null)
        {
            item.CreatedAt = createdAt.Value;
        }

        item.IsCompleted = completed;
        item.CompletedAt = completedAt;
        return item;
    }

    [Fact]
    public void Incomplete_ComesBeforeCompleted()
    {
        var done = Item("done", completed: true, completedAt: _clock.UtcNow);
        var open = Item("open");

        var ordered = TaskListOrder.Sort([done, open]);

        Assert.Equal("open", ordered[0].Title);
        Assert.Equal("done", ordered[1].Title);
    }

    [Fact]
    public void Incomplete_OrdersByPriorityThenDueThenCreatedDesc()
    {
        var t0 = _clock.UtcNow;
        var t1 = t0.AddMinutes(1);
        var today = _clock.Today;
        var tomorrow = today.AddDays(1);

        var lowEarly = Item("low-early", TaskPriority.Low, today, createdAt: t0);
        var highNoDue = Item("high-no-due", TaskPriority.High, createdAt: t0);
        var highDueTomorrow = Item("high-tomorrow", TaskPriority.High, tomorrow, createdAt: t0);
        var highDueTodayNewer = Item("high-today-newer", TaskPriority.High, today, createdAt: t1);
        var highDueTodayOlder = Item("high-today-older", TaskPriority.High, today, createdAt: t0);

        var ordered = TaskListOrder.Sort(
        [
            lowEarly,
            highNoDue,
            highDueTomorrow,
            highDueTodayNewer,
            highDueTodayOlder
        ]);

        Assert.Equal(
            new[] { "high-today-newer", "high-today-older", "high-tomorrow", "high-no-due", "low-early" },
            ordered.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void Completed_OrdersByCompletedAtDesc_IgnoringPriority()
    {
        var first = _clock.UtcNow;
        var second = first.AddMinutes(5);
        var third = first.AddMinutes(10);

        var highFirst = Item("high-first", TaskPriority.High, completed: true, completedAt: first);
        var lowLater = Item("low-later", TaskPriority.Low, completed: true, completedAt: second);
        var mediumLatest = Item("medium-latest", TaskPriority.Medium, completed: true, completedAt: third);

        var ordered = TaskListOrder.Sort([highFirst, mediumLatest, lowLater]);

        Assert.Equal(
            new[] { "medium-latest", "low-later", "high-first" },
            ordered.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void Completed_FallsBackWhenCompletedAtMissing()
    {
        var t0 = _clock.UtcNow;
        var t1 = t0.AddHours(1);

        var older = Item("older", completed: true, createdAt: t0);
        older.CompletedAt = null;
        older.ArchivedAt = t0;

        var newer = Item("newer", completed: true, createdAt: t1);
        newer.CompletedAt = null;
        newer.ArchivedAt = t1;

        var ordered = TaskListOrder.Sort([older, newer]);

        Assert.Equal(new[] { "newer", "older" }, ordered.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void Incomplete_PinnedRisesAboveHigherPriority()
    {
        var lowPinned = Item("low-pinned", TaskPriority.Low);
        lowPinned.IsPinned = true;
        lowPinned.PinnedAt = _clock.UtcNow;
        var high = Item("high", TaskPriority.High);

        var ordered = TaskListOrder.Sort([high, lowPinned]);

        Assert.Equal(new[] { "low-pinned", "high" }, ordered.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void Incomplete_LastPinnedIsFirst()
    {
        var earlier = _clock.UtcNow;
        var later = earlier.AddMinutes(5);
        var first = Item("first");
        first.IsPinned = true;
        first.PinnedAt = earlier;
        var second = Item("second");
        second.IsPinned = true;
        second.PinnedAt = later;
        var plain = Item("plain", TaskPriority.High);

        var ordered = TaskListOrder.Sort([plain, first, second]);

        Assert.Equal(new[] { "second", "first", "plain" }, ordered.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void Completed_IgnoresPin()
    {
        var t0 = _clock.UtcNow;
        var doneEarlier = Item("done-earlier", completed: true, completedAt: t0);
        doneEarlier.IsPinned = true;
        doneEarlier.PinnedAt = t0.AddHours(1);
        var doneLater = Item("done-later", TaskPriority.Low, completed: true, completedAt: t0.AddMinutes(5));
        var open = Item("open", TaskPriority.Low);

        var ordered = TaskListOrder.Sort([doneEarlier, doneLater, open]);

        Assert.Equal(new[] { "open", "done-later", "done-earlier" }, ordered.Select(t => t.Title).ToArray());
    }
}
