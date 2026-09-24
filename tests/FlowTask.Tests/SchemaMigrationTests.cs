using SQLite;
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

    private sealed class TableInfo
    {
        public string Name { get; set; } = string.Empty;
    }
}
