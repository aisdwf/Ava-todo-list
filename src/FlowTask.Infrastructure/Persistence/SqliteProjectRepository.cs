using SQLite;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Infrastructure.Persistence;

/// <summary>
/// 基于 SQLite 的项目仓储实现。
/// </summary>
public class SqliteProjectRepository : IProjectRepository
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// 构造项目仓储。
    /// </summary>
    /// <param name="dbPath">
    /// 数据库路径。必须与 <see cref="SqliteTaskRepository"/> 指向同一文件 ——
    /// <see cref="DeleteAsync"/> 需在单事务内同时改动 Tasks 与 Projects 两张表，
    /// 跨连接无法保证原子性。
    /// </param>
    public SqliteProjectRepository(string? dbPath = null)
    {
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

    /// <summary>
    /// 确保两张表均已就绪。
    /// </summary>
    /// <remarks>
    /// 同时建 <see cref="TaskItem"/> 表：<see cref="DeleteAsync"/> 要更新 Tasks，
    /// 若该表尚未创建（用户先建项目再建任务的路径）会抛表不存在异常。
    /// </remarks>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _semaphore.WaitAsync();
        try
        {
            if (!_initialized)
            {
                await _db.CreateTableAsync<Project>();
                await _db.CreateTableAsync<TaskItem>();
                _initialized = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<List<Project>> GetActiveProjectsAsync()
    {
        await InitializeAsync();
        return await _db.Table<Project>()
                        .Where(p => !p.IsArchived)
                        .OrderBy(p => p.SortOrder)
                        .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<Project>> GetAllProjectsAsync()
    {
        await InitializeAsync();
        return await _db.Table<Project>()
                        .OrderBy(p => p.SortOrder)
                        .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Project?> GetByIdAsync(string id)
    {
        await InitializeAsync();
        return await _db.Table<Project>()
                        .Where(p => p.Id == id)
                        .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<int> SaveProjectAsync(Project project)
    {
        await InitializeAsync();

        if (project.CreatedAt == default)
        {
            throw new InvalidOperationException(
                $"项目 {project.Id} 的 CreatedAt 未被赋值。创建方必须经 IClock 显式设置创建时间。");
        }

        var nameError = ProjectName.Validate(project.Name);
        if (nameError is not null)
        {
            throw new InvalidOperationException($"项目 {project.Id} 名称非法：{nameError}");
        }

        project.Name = ProjectName.Normalize(project.Name);

        var existing = await GetByIdAsync(project.Id);
        return existing is null
            ? await _db.InsertAsync(project)
            : await _db.UpdateAsync(project);
    }

    /// <inheritdoc />
    public async Task<int> SetArchivedAsync(string id, bool isArchived)
    {
        await InitializeAsync();
        var existing = await GetByIdAsync(id);
        if (existing is null) return 0;

        existing.IsArchived = isArchived;
        return await _db.UpdateAsync(existing);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>事务回调是同步上下文</b>：<c>RunInTransactionAsync</c> 的参数类型为
    /// <c>Action&lt;SQLiteConnection&gt;</c> —— 传入的是**同步**连接。
    /// 回调内若改用异步 API，操作会脱离该事务边界执行，
    /// 原子性静默失效且不报任何错。此处必须使用同步 <c>Execute</c> / <c>Delete</c>。
    /// </para>
    /// <para>
    /// 以 SQL 直接批量置空而非逐条 <c>Update</c>：避免先查询再回写的读改写竞态，
    /// 且单条语句天然原子。
    /// </para>
    /// </remarks>
    public async Task<int> DeleteAsync(string id)
    {
        await InitializeAsync();

        var affectedTasks = 0;

        await _db.RunInTransactionAsync(conn =>
        {
            affectedTasks = conn.Execute(
                "UPDATE Tasks SET ProjectId = NULL WHERE ProjectId = ?", id);

            conn.Delete<Project>(id);
        });

        return affectedTasks;
    }

    /// <inheritdoc />
    public async Task<int> CountTasksAsync(string projectId)
    {
        await InitializeAsync();
        return await _db.Table<TaskItem>()
                        .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                        .CountAsync();
    }
}
