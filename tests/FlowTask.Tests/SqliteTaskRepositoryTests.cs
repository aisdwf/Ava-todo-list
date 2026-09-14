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

    [Fact]
    public async Task GetAllActiveTasksAsync_ShouldFilterCompletedAndDeleted()
    {
        var task1 = NewTask("Active 1");
        var task2 = NewTask("Active 2");
        task2.IsCompleted = true;
        var task3 = NewTask("Active 3");
        task3.IsDeleted = true;

        await _repo.SaveTaskAsync(task1);
        await _repo.SaveTaskAsync(task2);
        await _repo.SaveTaskAsync(task3);

        var activeTasks = await _repo.GetAllActiveTasksAsync();
        Assert.Single(activeTasks);
        Assert.Equal("Active 1", activeTasks[0].Title);
    }

    [Fact]
    public async Task SoftDeleteAsync_ShouldMarkAsDeleted()
    {
        var task = NewTask("Delete me");
        await _repo.SaveTaskAsync(task);

        await _repo.SoftDeleteAsync(task.Id);

        var activeTasks = await _repo.GetAllActiveTasksAsync();
        Assert.Empty(activeTasks);

        var item = await _repo.GetByIdAsync(task.Id);
        Assert.NotNull(item);
        Assert.True(item.IsDeleted);
    }
}
