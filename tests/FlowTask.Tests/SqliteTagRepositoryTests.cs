using FlowTask.Core.Models;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

public class SqliteTagRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock =
        new(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
    private readonly SqliteTagRepository _tags;
    private readonly SqliteTaskRepository _tasks;

    public SqliteTagRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_tag_{Guid.NewGuid():N}.db");
        _tags = new SqliteTagRepository(_clock, _dbPath);
        _tasks = new SqliteTaskRepository(_clock, _dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task Initialize_SeedsBuiltInsIdempotently()
    {
        await _tags.InitializeAsync();
        await _tags.InitializeAsync();

        var tags = await _tags.GetAllAsync();

        Assert.Equal(TagCatalog.BuiltIns.Count, tags.Count);
        Assert.Equal(TagCatalog.BuiltIns.Select(definition => definition.Name), tags.Select(tag => tag.Name));
    }

    [Fact]
    public async Task Save_RejectsCaseInsensitiveDuplicateNames()
    {
        await _tags.InitializeAsync();
        var tag = new Tag
        {
            Name = "工作",
            CreatedAt = _clock.UtcNow
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tags.SaveAsync(tag));

        Assert.Contains("已存在", exception.Message);
    }

    [Fact]
    public async Task ReplaceTaskTags_ReplacesAndLoadsAssociations()
    {
        await _tags.InitializeAsync();
        var task = TaskItemFactory.Create(_clock, "任务");
        await _tasks.SaveTaskAsync(task);
        var allTags = await _tags.GetAllAsync();
        var selected = allTags.Take(2).ToList();

        await _tags.ReplaceTaskTagsAsync(task.Id, selected.Select(tag => tag.Id));

        var assigned = await _tags.GetTagsForTasksAsync(new[] { task.Id });

        Assert.Equal(selected.Select(tag => tag.Id), assigned[task.Id].Select(tag => tag.Id));
    }

    [Fact]
    public async Task Delete_RemovesAllTaskAssociations()
    {
        await _tags.InitializeAsync();
        var task = TaskItemFactory.Create(_clock, "任务");
        await _tasks.SaveTaskAsync(task);
        var tag = (await _tags.GetAllAsync()).First();
        await _tags.ReplaceTaskTagsAsync(task.Id, new[] { tag.Id });

        var removed = await _tags.DeleteAsync(tag.Id);

        Assert.Equal(1, removed);
        Assert.Empty((await _tags.GetTagsForTasksAsync(new[] { task.Id }))[task.Id]);
        Assert.Null((await _tags.GetAllAsync()).SingleOrDefault(item => item.Id == tag.Id));
    }

    [Fact]
    public async Task UsageCounts_OnlyCountUndeletedTasks()
    {
        await _tags.InitializeAsync();
        var tag = (await _tags.GetAllAsync()).First();
        var first = TaskItemFactory.Create(_clock, "任务一");
        var second = TaskItemFactory.Create(_clock, "任务二");
        await _tasks.SaveTaskAsync(first);
        await _tasks.SaveTaskAsync(second);
        await _tags.ReplaceTaskTagsAsync(first.Id, new[] { tag.Id });
        await _tags.ReplaceTaskTagsAsync(second.Id, new[] { tag.Id });

        await _tasks.SoftDeleteAsync(second.Id);

        var counts = await _tags.GetUsageCountsAsync();

        Assert.Equal(1, counts[tag.Id]);
    }
}
