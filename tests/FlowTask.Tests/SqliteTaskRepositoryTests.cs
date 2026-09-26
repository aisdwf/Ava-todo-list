using FlowTask.Core.Enums;
using FlowTask.Core.Models;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

public class SqliteTaskRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock;
    private readonly SqliteTaskRepository _repo;

    public SqliteTaskRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_test_{Guid.NewGuid():N}.db");
        _clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        _repo = new SqliteTaskRepository(_clock, _dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // Ignore cleanup lock in tests
            }
        }
    }

    /// <summary>
    /// 构造测试任务。
    /// </summary>
    /// <remarks>
    /// 经 <c>TaskItemFactory</c> 而非直接 <c>new</c>：<c>CreatedAt</c> 已改为
    /// 由创建方经 <c>IClock</c> 显式赋值（不再有字段初始化器兜底），
    /// 直接构造会触发仓储的快速失败断言。
    /// </remarks>
    private TaskItem NewTask(string title, TaskPriority priority = TaskPriority.Medium, DateTime? dueDate = null)
        => TaskItemFactory.Create(_clock, title, priority, dueDate: dueDate);

    [Fact]
    public async Task SaveTaskAsync_ShouldInsertAndRetrieveTask()
    {
        var task = TaskItemFactory.Create(
            _clock,
            "买牛奶",
            TaskPriority.High,
            dueDate: _clock.Today.AddDays(1),
            description: "低脂鲜牛奶 1L");

        var rows = await _repo.SaveTaskAsync(task);
        Assert.Equal(1, rows);

        var retrieved = await _repo.GetByIdAsync(task.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("买牛奶", retrieved.Title);
        Assert.Equal(TaskPriority.High, retrieved.Priority);
    }

    /// <summary>
    /// 已完成任务仍出现在看板；历史 <c>IsArchived</c> 不再把它们藏起来；软删行仍排除。
    /// </summary>
    [Fact]
    public async Task GetAllActiveTasksAsync_IncludesCompletedAndPreviouslyArchived_ExcludesDeleted()
    {
        var task1 = NewTask("Active 1");
        var task2 = NewTask("Completed");
        task2.IsCompleted = true;
        var task3 = NewTask("Deleted");
        task3.IsDeleted = true;
        var task4 = NewTask("Previously archived");
        task4.IsCompleted = true;
        task4.IsArchived = true;

        await _repo.SaveTaskAsync(task1);
        await _repo.SaveTaskAsync(task2);
        await _repo.SaveTaskAsync(task3);
        await _repo.SaveTaskAsync(task4);

        var activeTasks = await _repo.GetAllActiveTasksAsync();
        Assert.Equal(3, activeTasks.Count);
        Assert.Contains(activeTasks, t => t.Title == "Active 1");
        Assert.Contains(activeTasks, t => t.Title == "Completed");
        Assert.Contains(activeTasks, t => t.Title == "Previously archived");
        Assert.DoesNotContain(activeTasks, t => t.Title == "Deleted");
    }

    [Fact]
    public async Task PermanentDeleteAsync_RemovesRow()
    {
        var task = NewTask("Delete me");
        await _repo.SaveTaskAsync(task);

        await _repo.PermanentDeleteAsync(task.Id);

        var activeTasks = await _repo.GetAllActiveTasksAsync();
        Assert.Empty(activeTasks);
        Assert.Null(await _repo.GetByIdAsync(task.Id));
    }

    [Fact]
    public async Task GetAllActiveTasksAsync_SortsIncompleteThenCompleted()
    {
        var low = NewTask("low", TaskPriority.Low);
        var high = NewTask("high", TaskPriority.High);
        var done = NewTask("done", TaskPriority.High);
        done.IsCompleted = true;
        done.CompletedAt = _clock.UtcNow;

        await _repo.SaveTaskAsync(low);
        await _repo.SaveTaskAsync(high);
        await _repo.SaveTaskAsync(done);

        var titles = (await _repo.GetAllActiveTasksAsync()).Select(t => t.Title).ToArray();
        Assert.Equal(new[] { "high", "low", "done" }, titles);
    }
}
