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
    /// 完成 ≠ 归档（spec-task-complete-before-archive）：已完成但未归档的任务
    /// 仍应出现在活动列表中；只有已归档或已删除的任务才被排除。
    /// </summary>
    [Fact]
    public async Task GetAllActiveTasksAsync_ShouldIncludeCompletedButExcludeArchivedAndDeleted()
    {
        var task1 = NewTask("Active 1");
        var task2 = NewTask("Completed not archived");
        task2.IsCompleted = true;
        var task3 = NewTask("Deleted");
        task3.IsDeleted = true;
        var task4 = NewTask("Archived");
        task4.IsCompleted = true;
        task4.IsArchived = true;

        await _repo.SaveTaskAsync(task1);
        await _repo.SaveTaskAsync(task2);
        await _repo.SaveTaskAsync(task3);
        await _repo.SaveTaskAsync(task4);

        var activeTasks = await _repo.GetAllActiveTasksAsync();
        Assert.Equal(2, activeTasks.Count);
        Assert.Contains(activeTasks, t => t.Title == "Active 1");
        Assert.Contains(activeTasks, t => t.Title == "Completed not archived");
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

    [Fact]
    public async Task ArchiveAllCompletedAsync_OnlyArchivesCompletedNotYetArchived()
    {
        var completedNotArchived = NewTask("Completed not archived");
        completedNotArchived.IsCompleted = true;
        var notCompleted = NewTask("Not completed");
        var alreadyArchived = NewTask("Already archived");
        alreadyArchived.IsCompleted = true;
        alreadyArchived.IsArchived = true;

        await _repo.SaveTaskAsync(completedNotArchived);
        await _repo.SaveTaskAsync(notCompleted);
        await _repo.SaveTaskAsync(alreadyArchived);

        var affected = await _repo.ArchiveAllCompletedAsync();
        Assert.Equal(1, affected);

        var reloaded = await _repo.GetByIdAsync(completedNotArchived.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.IsArchived);
        Assert.NotNull(reloaded.ArchivedAt);

        var stillActive = await _repo.GetByIdAsync(notCompleted.Id);
        Assert.NotNull(stillActive);
        Assert.False(stillActive.IsArchived);

        var activeTasks = await _repo.GetAllActiveTasksAsync();
        Assert.DoesNotContain(activeTasks, t => t.Id == completedNotArchived.Id);
        Assert.Contains(activeTasks, t => t.Id == notCompleted.Id);

        var archivedView = await _repo.GetCompletedTasksAsync();
        Assert.Contains(archivedView, t => t.Id == completedNotArchived.Id);
        Assert.Contains(archivedView, t => t.Id == alreadyArchived.Id);
    }

    /// <summary>
    /// D4：升级前既有的 <c>IsCompleted=true</c> 历史行（新列 <c>IsArchived</c> 默认 false）
    /// 必须在首次 <c>InitializeAsync</c> 后被自动标记为已归档，避免突然挤回活动列表。
    /// </summary>
    [Fact]
    public async Task InitializeAsync_MigratesHistoricalCompletedRowsToArchived()
    {
        var historical = NewTask("Historical completed");
        await _repo.SaveTaskAsync(historical);

        // 模拟旧版本产生的数据：IsCompleted=true 但 IsArchived 仍为默认值 false，
        // 直接改字段绕过仓储的 InitializeAsync 迁移逻辑，还原「升级前」状态
        historical.IsCompleted = true;
        await _repo.SaveTaskAsync(historical);

        // 新建一个指向同一数据库文件的仓储实例，模拟应用重启触发的 InitializeAsync
        var restarted = new SqliteTaskRepository(_clock, _dbPath);
        var activeAfterRestart = await restarted.GetAllActiveTasksAsync();
        Assert.DoesNotContain(activeAfterRestart, t => t.Id == historical.Id);

        var archivedAfterRestart = await restarted.GetCompletedTasksAsync();
        var migrated = Assert.Single(archivedAfterRestart, t => t.Id == historical.Id);
        Assert.True(migrated.IsArchived);
        Assert.Equal(migrated.CreatedAt, migrated.ArchivedAt);
    }
}
