using SQLite;
using FlowTask.Core.Models;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 验证既有数据库在契约扩展后仍然可用。
/// </summary>
/// <remarks>
/// <para>
/// DESIGN-0004 §2.1 依据实测断定「新增列无需迁移脚本」。
/// 那次验证在独立临时项目中进行，此处将其固化为回归测试 ——
/// 否则该结论只是一次性的口头证据，日后升级 <c>sqlite-net-pcl</c>
/// 若行为变化，将无任何机制发现。
/// </para>
/// <para>
/// 用户真实的 <c>flowtask.db</c> 中存有旧 schema 的数据，
/// 本用例模拟该升级路径。
/// </para>
/// </remarks>
public class SchemaMigrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock;

    public SchemaMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_mig_{Guid.NewGuid():N}.db");
        _clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
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
                // 清理阶段的文件锁不影响断言结果
            }
        }
    }

    /// <summary>
    /// 契约扩展前的历史任务实体（无 ProjectId / Tags）。
    /// </summary>
    /// <remarks>
    /// 刻意映射到同一张 <c>Tasks</c> 表，以复现真实的版本升级场景。
    /// </remarks>
    [Table("Tasks")]
    private class LegacyTaskItem
    {
        [PrimaryKey] public string Id { get; set; } = Guid.NewGuid().ToString("N");
        [Indexed] public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsCompleted { get; set; }
        public int Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsDeleted { get; set; }
    }

    [Fact]
    public async Task ExistingDatabase_IsUpgradedWithoutDataLoss()
    {
        var legacyId = Guid.NewGuid().ToString("N");

        // 阶段一：以旧 schema 建库并写入历史数据
        {
            var legacyDb = new SQLiteAsyncConnection(_dbPath);
            await legacyDb.CreateTableAsync<LegacyTaskItem>();
            await legacyDb.InsertAsync(new LegacyTaskItem
            {
                Id = legacyId,
                Title = "升级前就存在的任务",
                Priority = 2,
                CreatedAt = _clock.UtcNow.AddDays(-30)
            });
            await legacyDb.CloseAsync();
        }

        // 阶段二：以新 schema 打开同一文件
        var repo = new SqliteTaskRepository(_clock, _dbPath);
        var stored = await repo.GetByIdAsync(legacyId);

        // 历史数据完整保留
        Assert.NotNull(stored);
        Assert.Equal("升级前就存在的任务", stored.Title);
        Assert.Equal(_clock.UtcNow.AddDays(-30), stored.CreatedAt);

        // 新列取默认值，落入「未归属 / 无标签」这一正常状态
        Assert.Null(stored.ProjectId);
        Assert.Null(stored.Tags);
    }

    /// <summary>
    /// 升级后历史任务仍可被正常查询与修改。
    /// </summary>
    [Fact]
    public async Task LegacyTask_RemainsEditableAfterUpgrade()
    {
        var legacyId = Guid.NewGuid().ToString("N");

        {
            var legacyDb = new SQLiteAsyncConnection(_dbPath);
            await legacyDb.CreateTableAsync<LegacyTaskItem>();
            await legacyDb.InsertAsync(new LegacyTaskItem
            {
                Id = legacyId,
                Title = "历史任务",
                CreatedAt = _clock.UtcNow.AddDays(-10)
            });
            await legacyDb.CloseAsync();
        }

        var repo = new SqliteTaskRepository(_clock, _dbPath);

        Assert.Single(await repo.GetAllActiveTasksAsync());

        // 为历史任务补上新维度
        var task = await repo.GetByIdAsync(legacyId);
        task!.ProjectId = "proj-new";
        task.Tags = TagNormalizer.Normalize(new[] { "补充" });
        task.DueDate = _clock.Today;
        await repo.SaveTaskAsync(task);

        var updated = await repo.GetByIdAsync(legacyId);
        Assert.Equal("proj-new", updated!.ProjectId);
        Assert.Equal("补充", updated.Tags);
        Assert.Single(await repo.GetTodayTasksAsync());
    }

    /// <summary>
    /// Projects 表在旧库中不存在，须能被自动创建。
    /// </summary>
    [Fact]
    public async Task ProjectsTable_IsCreatedOnExistingDatabase()
    {
        {
            var legacyDb = new SQLiteAsyncConnection(_dbPath);
            await legacyDb.CreateTableAsync<LegacyTaskItem>();
            await legacyDb.CloseAsync();
        }

        var projects = new SqliteProjectRepository(_dbPath);
        var project = new Project { Name = "升级后新建", CreatedAt = _clock.UtcNow };

        await projects.SaveProjectAsync(project);

        Assert.Single(await projects.GetActiveProjectsAsync());
    }
}
