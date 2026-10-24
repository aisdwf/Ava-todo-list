using FlowTask.Core.Models;
using FlowTask.Desktop.ViewModels;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 任务行展示投影（spec-due-date-picker §C）。
/// </summary>
public class TaskRowViewModelTests
{
    /// <summary>
    /// 创建日期按本地时区显示：此前直接格式化 UTC，本地凌晨创建的任务会显示成前一天。
    /// </summary>
    [Fact]
    public void CreatedLocalText_ConvertsUtcToLocalDate()
    {
        var createdUtc = new DateTime(2026, 3, 9, 20, 30, 0, DateTimeKind.Utc);
        var task = new TaskItem { Title = "t", CreatedAt = DateTime.SpecifyKind(createdUtc, DateTimeKind.Unspecified) };
        var row = new TaskRowViewModel(task, null);

        var expected = createdUtc.ToLocalTime().ToString("MM/dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, row.CreatedLocalText);
    }

    /// <summary>
    /// 已完成任务展示「创建日~完成日 已完成」（本地时区）。同一天仍写全区间；缺完成时刻时只显示创建日。
    /// </summary>
    [Fact]
    public void CompletedLocalText_ShowsCreatedToCompletedSpan()
    {
        var createdUtc = new DateTime(2026, 3, 1, 4, 0, 0, DateTimeKind.Utc);
        var completedUtc = new DateTime(2026, 3, 9, 20, 30, 0, DateTimeKind.Utc);
        var task = new TaskItem
        {
            Title = "t",
            CreatedAt = DateTime.SpecifyKind(createdUtc, DateTimeKind.Unspecified),
            DueDate = new DateTime(2026, 3, 1),
            IsCompleted = true,
            CompletedAt = DateTime.SpecifyKind(completedUtc, DateTimeKind.Unspecified)
        };
        var row = new TaskRowViewModel(task, null);

        var created = createdUtc.ToLocalTime().ToString("MM/dd", System.Globalization.CultureInfo.InvariantCulture);
        var completed = completedUtc.ToLocalTime().ToString("MM/dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal($"{created}~{completed} 已完成", row.CompletedLocalText);

        task.CompletedAt = createdUtc;
        Assert.Equal($"{created}~{created} 已完成", row.CompletedLocalText);

        task.CompletedAt = null;
        Assert.Equal($"{created} 已完成", row.CompletedLocalText);
    }

    /// <summary>
    /// 勾选 / 取消勾选时通知完成日文案，否则行上仍停留在旧展示。
    /// </summary>
    [Fact]
    public void ToggleCompleted_NotifiesCompletedText()
    {
        var task = new TaskItem { Title = "t", CreatedAt = new DateTime(2026, 3, 10) };
        var row = new TaskRowViewModel(task, null);
        var notified = new List<string?>();
        row.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        row.IsCompleted = true;
        row.ApplyPersistedCompletion(true, new DateTime(2026, 3, 10, 1, 0, 0));

        Assert.Equal(2, notified.Count(n => n == nameof(TaskRowViewModel.CompletedLocalText)));
    }

    /// <summary>
    /// 到期日投影随实体变化并发出通知，否则选择器触发入口停留在旧文案。
    /// </summary>
    [Fact]
    public void RefreshDerivedFlags_NotifiesDueDate()
    {
        var task = new TaskItem { Title = "t", CreatedAt = new DateTime(2026, 3, 10) };
        var row = new TaskRowViewModel(task, null);
        var notified = new List<string?>();
        row.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        task.DueDate = new DateTime(2026, 3, 12);
        row.RefreshDerivedFlags();

        Assert.Equal(new DateTime(2026, 3, 12), row.DueDate);
        Assert.Contains(nameof(TaskRowViewModel.DueDate), notified);
        Assert.Contains(nameof(TaskRowViewModel.HasDueDate), notified);
    }
}
