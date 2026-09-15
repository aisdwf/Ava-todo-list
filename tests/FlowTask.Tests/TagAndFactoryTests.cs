using FlowTask.Core.Enums;
using FlowTask.Core.Models;
using Xunit;

namespace FlowTask.Tests;

public class TagNameTests
{
    [Fact]
    public void Validate_RejectsBlank()
    {
        Assert.NotNull(TagName.Validate(null));
        Assert.NotNull(TagName.Validate("   "));
    }

    [Fact]
    public void Validate_EnforcesMaxLength()
    {
        Assert.Null(TagName.Validate(new string('a', TagName.MaxLength)));
        Assert.NotNull(TagName.Validate(new string('a', TagName.MaxLength + 1)));
    }

    [Fact]
    public void Normalize_TrimsAndCollapsesWhitespace()
        => Assert.Equal("线上 修复", TagName.Normalize("  线上    修复  "));
}

public class TaskItemFactoryTests
{
    private static readonly FakeClock Clock =
        new(new DateTime(2026, 3, 10, 8, 30, 0, DateTimeKind.Utc));

    [Fact]
    public void Create_StampsCreatedAtFromClock()
        => Assert.Equal(
            Clock.UtcNow,
            TaskItemFactory.Create(Clock, "写测试").CreatedAt);

    [Fact]
    public void Create_TrimsTitle()
        => Assert.Equal("写测试", TaskItemFactory.Create(Clock, "  写测试  ").Title);

    [Fact]
    public void Create_PreservesSpecialCharactersInTitle()
        => Assert.Equal(
            "修 bug #FlowTask @紧急",
            TaskItemFactory.Create(Clock, "修 bug #FlowTask @紧急").Title);

    [Fact]
    public void Create_LeavesClassificationEmptyByDefault()
    {
        var task = TaskItemFactory.Create(Clock, "随手记");

        Assert.Null(task.ProjectId);
        Assert.Null(task.DueDate);
        Assert.Null(task.Description);
        Assert.Equal(TaskPriority.Medium, task.Priority);
        Assert.False(task.IsCompleted);
        Assert.False(task.IsDeleted);
    }

    [Fact]
    public void Create_AcceptsFullNonTagClassification()
    {
        var due = new DateTime(2026, 3, 15);
        var task = TaskItemFactory.Create(
            Clock,
            "完整任务",
            TaskPriority.High,
            projectId: "proj-1",
            dueDate: due,
            description: "备注");

        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal("proj-1", task.ProjectId);
        Assert.Equal(due, task.DueDate);
        Assert.Equal("备注", task.Description);
    }

    [Fact]
    public void Create_GeneratesDistinctIds()
    {
        var a = TaskItemFactory.Create(Clock, "甲");
        var b = TaskItemFactory.Create(Clock, "乙");

        Assert.NotEqual(a.Id, b.Id);
    }
}
