using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.ViewModels.Actions;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

public class TaskPinTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock = new(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
    private readonly SqliteTaskRepository _repo;

    public TaskPinTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_pin_{Guid.NewGuid():N}.db");
        _repo = new SqliteTaskRepository(_clock, _dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task TogglePin_StampsClockAndRoundsTrip()
    {
        var task = await Save("突然重要");
        var sut = new TogglePinTaskViewModel(_repo, _clock);

        await sut.ExecuteAsync(task, () => Task.CompletedTask, this);

        var stored = await _repo.GetByIdAsync(task.Id);
        Assert.NotNull(stored);
        Assert.True(stored.IsPinned);
        Assert.Equal(_clock.UtcNow, stored.PinnedAt);
    }

    [Fact]
    public async Task TogglePin_SecondClickClears()
    {
        var task = await Save("临时顶上");
        var sut = new TogglePinTaskViewModel(_repo, _clock);
        await sut.ExecuteAsync(task, () => Task.CompletedTask, this);

        await sut.ExecuteAsync(task, () => Task.CompletedTask, this);

        var stored = await _repo.GetByIdAsync(task.Id);
        Assert.NotNull(stored);
        Assert.False(stored.IsPinned);
        Assert.Null(stored.PinnedAt);
    }

    [Fact]
    public async Task TogglePin_CompletedTaskIsIgnored()
    {
        var task = await Save("已做完");
        task.IsCompleted = true;
        task.CompletedAt = _clock.UtcNow;
        await _repo.SaveTaskAsync(task);

        await new TogglePinTaskViewModel(_repo, _clock)
            .ExecuteAsync(task, () => Task.CompletedTask, this);

        var stored = await _repo.GetByIdAsync(task.Id);
        Assert.NotNull(stored);
        Assert.False(stored.IsPinned);
        Assert.Null(stored.PinnedAt);
    }

    [Fact]
    public async Task ToggleComplete_ClearsPin_UncompleteDoesNotRestore()
    {
        var task = await Save("做完就取消置顶");
        var pin = new TogglePinTaskViewModel(_repo, _clock);
        await pin.ExecuteAsync(task, () => Task.CompletedTask, this);
        var pinnedAt = task.PinnedAt;

        task.IsCompleted = true;
        var complete = new ToggleCompleteTaskViewModel(_repo, _clock);
        await complete.ExecuteAsync(task, () => Task.CompletedTask, this);

        var done = await _repo.GetByIdAsync(task.Id);
        Assert.NotNull(done);
        Assert.True(done.IsCompleted);
        Assert.False(done.IsPinned);
        Assert.Null(done.PinnedAt);
        Assert.NotEqual(pinnedAt, done.PinnedAt);

        done.IsCompleted = false;
        await complete.ExecuteAsync(done, () => Task.CompletedTask, this);

        var reopened = await _repo.GetByIdAsync(task.Id);
        Assert.NotNull(reopened);
        Assert.False(reopened.IsCompleted);
        Assert.False(reopened.IsPinned);
        Assert.Null(reopened.PinnedAt);
    }

    [Fact]
    public async Task TogglePin_RestoresWhenSaveThrows()
    {
        var task = TaskItemFactory.Create(_clock, "失败回滚", projectId: DefaultProject.Id);
        task.IsPinned = true;
        task.PinnedAt = _clock.UtcNow.AddMinutes(-3);
        var sut = new TogglePinTaskViewModel(new ThrowingTaskRepository(), _clock);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(task, () => Task.CompletedTask, this));

        Assert.True(task.IsPinned);
        Assert.Equal(_clock.UtcNow.AddMinutes(-3), task.PinnedAt);
    }

    private async Task<TaskItem> Save(string title)
    {
        var task = TaskItemFactory.Create(_clock, title, projectId: DefaultProject.Id);
        await _repo.SaveTaskAsync(task);
        return task;
    }

    private sealed class ThrowingTaskRepository : ITaskRepository
    {
        public Task<List<TaskItem>> GetAllActiveTasksAsync() => Task.FromResult(new List<TaskItem>());
        public Task<List<TaskItem>> GetAllNonDeletedTasksAsync() => Task.FromResult(new List<TaskItem>());
        public Task<List<TaskItem>> GetTodayTasksAsync() => Task.FromResult(new List<TaskItem>());
        public Task<List<TaskItem>> GetTasksByProjectAsync(string? projectId) => Task.FromResult(new List<TaskItem>());
        public Task<TaskItem?> GetByIdAsync(string id) => Task.FromResult<TaskItem?>(null);
        public Task<int> SaveTaskAsync(TaskItem item) => throw new InvalidOperationException("fail");
        public Task<int> PermanentDeleteAsync(string id) => Task.FromResult(0);
        public Task<int> CountActiveTasksAsync() => Task.FromResult(0);
        public Task<IReadOnlyDictionary<string, int>> CountTasksGroupedByProjectAsync()
            => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
    }
}
