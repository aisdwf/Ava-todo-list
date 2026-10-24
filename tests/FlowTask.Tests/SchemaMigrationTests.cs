using SQLite;
using FlowTask.Core.Models;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

public class SchemaMigrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock =
        new(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));

    public SchemaMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_schema_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    /// <summary>
    /// 回归防护（spec-remove-tag-feature）：标签功能已完全移除，新数据库
    /// 不应再创建 <c>Tags</c>/<c>TaskTags</c> 表 —— 若未来有代码不慎重新引入
    /// 标签表创建逻辑，本测试应失败并提醒违反了移除决策。
    /// </summary>
    [Fact]
    public async Task NewSchema_CreatesTaskTable_ButNotTagTables()
    {
        var tasks = new SqliteTaskRepository(_clock, _dbPath);
        await tasks.InitializeAsync();

        var connection = new SQLiteAsyncConnection(_dbPath);
        var tables = await connection.QueryAsync<TableInfo>(
            "SELECT name AS Name FROM sqlite_master WHERE type = 'table'");

        Assert.Contains(tables, table => table.Name == "Tasks");
        Assert.DoesNotContain(tables, table => table.Name == "Tags");
        Assert.DoesNotContain(tables, table => table.Name == "TaskTags");
    }

    /// <summary>
    /// 回归防护（spec-theme-bound-decoration-colors）：<c>Project.ColorHex</c> 已移除，
    /// 但旧库的 Projects 表仍带该列。仓储不做列迁移，必须在旧表上照常种子、新建与读回；
    /// 读回时旧行里的颜色值被忽略。
    /// </summary>
    [Fact]
    public async Task LegacyProjectsTableWithColorHex_StillSeedsCreatesAndReads()
    {
        var legacy = new SQLiteAsyncConnection(_dbPath);
        // 与旧版 CreateTableAsync<Project>() 建出的列一致：ColorHex 无 NOT NULL 约束
        await legacy.ExecuteAsync(
            """
            CREATE TABLE Projects (
                Id varchar PRIMARY KEY NOT NULL,
                Name varchar,
                ColorHex varchar,
                SortOrder integer,
                IsArchived integer,
                CreatedAt bigint)
            """);
        await legacy.ExecuteAsync(
            "INSERT INTO Projects (Id, Name, ColorHex, SortOrder, IsArchived, CreatedAt) VALUES (?, ?, ?, ?, ?, ?)",
            "legacy-1", "旧项目", "#A78BFA", 1, false, _clock.UtcNow.Ticks);
        await legacy.CloseAsync();

        var projects = new SqliteProjectRepository(_dbPath);
        await projects.EnsureDefaultProjectAsync(_clock.UtcNow);
        await projects.SaveProjectAsync(new Project
        {
            Name = "新项目",
            SortOrder = await projects.NextSortOrderAsync(),
            CreatedAt = _clock.UtcNow
        });

        var all = await projects.GetAllProjectsAsync();
        Assert.Contains(all, p => p.Id == DefaultProject.Id);
        Assert.Contains(all, p => p.Id == "legacy-1" && p.Name == "旧项目");
        Assert.Contains(all, p => p.Name == "新项目");
    }

    /// <summary>
    /// 置顶两列是后加的。旧库没有这两列时，启动补列且既有任务保持未置顶（spec-task-pin）。
    /// </summary>
    [Fact]
    public async Task LegacyTasksTableWithoutPinColumns_ReadsAsUnpinned()
    {
        var created = _clock.UtcNow;
        var legacy = new SQLiteAsyncConnection(_dbPath);
        await legacy.ExecuteAsync(
            """
            CREATE TABLE Tasks (
                Id varchar PRIMARY KEY NOT NULL,
                Title varchar,
                Description varchar,
                IsCompleted integer NOT NULL,
                Priority integer NOT NULL,
                ProjectId varchar,
                DueDate bigint,
                CreatedAt bigint NOT NULL,
                CompletedAt bigint,
                IsArchived integer NOT NULL,
                ArchivedAt bigint,
                IsDeleted integer NOT NULL,
                IsCliWritten integer NOT NULL)
            """);
        await legacy.ExecuteAsync(
            """
            INSERT INTO Tasks (
                Id, Title, Description, IsCompleted, Priority, ProjectId, DueDate,
                CreatedAt, CompletedAt, IsArchived, ArchivedAt, IsDeleted, IsCliWritten)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            "legacy-task", "旧任务", null, false, 1, DefaultProject.Id, null,
            created.Ticks, null, false, null, false, false);
        await legacy.CloseAsync();

        var tasks = new SqliteTaskRepository(_clock, _dbPath);
        var stored = await tasks.GetByIdAsync("legacy-task");

        Assert.NotNull(stored);
        Assert.Equal("旧任务", stored.Title);
        Assert.False(stored.IsPinned);
        Assert.Null(stored.PinnedAt);
    }

    private sealed class TableInfo
    {
        public string Name { get; set; } = string.Empty;
    }
}
