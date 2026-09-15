using SQLite;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Infrastructure.Persistence;

/// <summary>
/// 基于 SQLite 的标签仓储实现。
/// </summary>
public sealed class SqliteTagRepository : ITagRepository
{
    private readonly SQLiteAsyncConnection _db;
    private readonly IClock _clock;
    private bool _initialized;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// 构造标签仓储。
    /// </summary>
    /// <param name="clock">时间提供者，用于内置标签的创建时刻。</param>
    /// <param name="dbPath">数据库路径；为空时使用 FlowTask 默认路径。</param>
    public SqliteTagRepository(IClock clock, string? dbPath = null)
    {
        _clock = clock;

        if (string.IsNullOrEmpty(dbPath))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "FlowTask");
            Directory.CreateDirectory(folder);
            dbPath = Path.Combine(folder, "flowtask.db");
        }
        else
        {
            var dbDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }
        }

        _db = new SQLiteAsyncConnection(dbPath);
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _semaphore.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            await _db.CreateTableAsync<TaskItem>();
            await _db.CreateTableAsync<Tag>();
            await _db.CreateTableAsync<TaskTag>();
            await _db.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_Tags_Name_NoCase ON Tags(Name COLLATE NOCASE)");
            await _db.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_TaskTags_Task_Tag ON TaskTags(TaskId, TagId)");

            await EnsureBuiltInsAsync();
            _initialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<List<Tag>> GetAllAsync()
    {
        await InitializeAsync();
        return await _db.Table<Tag>()
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, List<Tag>>> GetTagsForTasksAsync(IEnumerable<string> taskIds)
    {
        await InitializeAsync();

        var requestedIds = taskIds.ToHashSet(StringComparer.Ordinal);
        if (requestedIds.Count == 0)
        {
            return new Dictionary<string, List<Tag>>(StringComparer.Ordinal);
        }

        var assignments = await _db.Table<TaskTag>().ToListAsync();
        var tags = await _db.Table<Tag>().ToListAsync();
        var tagLookup = tags.ToDictionary(tag => tag.Id, StringComparer.Ordinal);
        var result = requestedIds.ToDictionary(
            taskId => taskId,
            _ => new List<Tag>(),
            StringComparer.Ordinal);

        foreach (var assignment in assignments)
        {
            if (requestedIds.Contains(assignment.TaskId)
                && tagLookup.TryGetValue(assignment.TagId, out var tag))
            {
                result[assignment.TaskId].Add(tag);
            }
        }

        foreach (var taskTags in result.Values)
        {
            taskTags.Sort(static (left, right) =>
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : StringComparer.CurrentCulture.Compare(left.Name, right.Name);
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, int>> GetUsageCountsAsync()
    {
        await InitializeAsync();

        var rows = await _db.QueryAsync<TagUsageRow>(
            """
            SELECT TaskTags.TagId, COUNT(*) AS UsageCount
            FROM TaskTags
            INNER JOIN Tasks ON Tasks.Id = TaskTags.TaskId
            WHERE Tasks.IsDeleted = 0
            GROUP BY TaskTags.TagId
            """);

        return rows.ToDictionary(row => row.TagId, row => row.UsageCount, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<int> SaveAsync(Tag tag)
    {
        await InitializeAsync();

        if (tag.CreatedAt == default)
        {
            throw new InvalidOperationException(
                $"标签 {tag.Id} 的 CreatedAt 未被赋值。创建方必须经 IClock 显式设置创建时间。");
        }

        var nameError = TagName.Validate(tag.Name);
        if (nameError is not null)
        {
            throw new InvalidOperationException($"标签 {tag.Id} 名称非法：{nameError}");
        }

        tag.Name = TagName.Normalize(tag.Name);
        var existing = await _db.Table<Tag>()
            .Where(candidate => candidate.Id != tag.Id)
            .ToListAsync();

        if (existing.Any(candidate =>
                string.Equals(candidate.Name, tag.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"标签名称「{tag.Name}」已存在。");
        }

        var stored = await _db.Table<Tag>()
            .Where(candidate => candidate.Id == tag.Id)
            .FirstOrDefaultAsync();

        return stored is null
            ? await _db.InsertAsync(tag)
            : await _db.UpdateAsync(tag);
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync(string tagId)
    {
        await InitializeAsync();

        var affected = 0;
        await _db.RunInTransactionAsync(connection =>
        {
            affected = connection.Execute("DELETE FROM TaskTags WHERE TagId = ?", tagId);
            connection.Delete<Tag>(tagId);
        });

        return affected;
    }

    /// <inheritdoc />
    public async Task ReplaceTaskTagsAsync(string taskId, IEnumerable<string> tagIds)
    {
        await InitializeAsync();

        var distinctIds = tagIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var knownIds = (await _db.Table<Tag>().ToListAsync())
            .Select(tag => tag.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (distinctIds.Any(id => !knownIds.Contains(id)))
        {
            throw new InvalidOperationException("任务标签包含不存在的标签。");
        }

        await _db.RunInTransactionAsync(connection =>
        {
            connection.Execute("DELETE FROM TaskTags WHERE TaskId = ?", taskId);
            foreach (var tagId in distinctIds)
            {
                connection.Insert(new TaskTag { TaskId = taskId, TagId = tagId });
            }
        });
    }

    private async Task EnsureBuiltInsAsync()
    {
        var existing = await _db.Table<Tag>().ToListAsync();
        foreach (var definition in TagCatalog.BuiltIns)
        {
            if (existing.Any(tag =>
                    string.Equals(tag.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            await _db.InsertAsync(new Tag
            {
                Name = definition.Name,
                ColorHex = definition.ColorHex,
                SortOrder = definition.SortOrder,
                IsBuiltIn = true,
                CreatedAt = _clock.UtcNow
            });
        }
    }

    private sealed class TagUsageRow
    {
        public string TagId { get; set; } = string.Empty;
        public int UsageCount { get; set; }
    }
}
