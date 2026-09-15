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

    [Fact]
    public async Task NewSchema_CreatesTaskAndTagTables()
    {
        var tags = new SqliteTagRepository(_clock, _dbPath);
        await tags.InitializeAsync();

        var connection = new SQLiteAsyncConnection(_dbPath);
        var tables = await connection.QueryAsync<TableInfo>(
            "SELECT name AS Name FROM sqlite_master WHERE type = 'table'");

        Assert.Contains(tables, table => table.Name == "Tasks");
        Assert.Contains(tables, table => table.Name == "Tags");
        Assert.Contains(tables, table => table.Name == "TaskTags");
    }

    private sealed class TableInfo
    {
        public string Name { get; set; } = string.Empty;
    }
}
