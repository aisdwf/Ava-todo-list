using SQLite;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Infrastructure.Persistence;

/// <summary>
/// 基于 SQLite 的待办事项仓储实现。
/// </summary>
public class SqliteTaskRepository : ITaskRepository
{
    private readonly SQLiteAsyncConnection _db;
    private readonly IClock _clock;
    private bool _initialized;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// 构造任务仓储。
    /// </summary>
    /// <param name="clock">时间提供者。「今日」区间依赖它，不可直接读系统时钟（Article 9）。</param>
    /// <param name="dbPath">数据库路径；为空时落在用户本地应用数据目录。</param>
    public SqliteTaskRepository(IClock clock, string? dbPath = null)
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

    /// <summary>
    /// 确保数据表已就绪。
    /// </summary>
    /// <remarks>
    /// <c>CreateTableAsync</c> 对已存在的表会自动执行 <c>ALTER TABLE ADD COLUMN</c>
    /// 补齐新增列（返回 <c>Migrated</c>），既有行保留、新列取默认值。
    /// 故本项目新增字段无需迁移脚本。此行为已实测确认（见 design-domain-contract §2.1）。
    /// </remarks>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _semaphore.WaitAsync();
        try
        {
            if (!_initialized)
            {
                await _db.CreateTableAsync<TaskItem>();
                await MigrateHistoricalCompletedToArchivedAsync();
                _initialized = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// D4 一次性数据校正（spec-task-complete-before-archive）：
    /// <c>IsArchived</c> 列是本轮新增字段，历史行的默认值为 <c>false</c>。
    /// 若不做这一步，升级前所有已勾选完成的任务会在活动列表「已完成未归档」区间突然重新出现，
    /// 与用户认知（这些任务早已在旧版本的「已完成归档」视图里）相悖。
    /// </summary>
    /// <remarks>
    /// 幂等：只对 <c>IsCompleted &amp;&amp; !IsArchived</c> 的历史行生效；
    /// 校正后这些行的 <c>IsArchived</c> 变为 <c>true</c>，重复调用不会再匹配到它们。
    /// 归档时刻无法还原真实归档发生的那一刻，取 <c>CreatedAt</c> 占位而非读系统时钟（Article 9）。
    /// </remarks>
    private async Task MigrateHistoricalCompletedToArchivedAsync()
    {
        var staleCompleted = await _db.Table<TaskItem>()
            .Where(t => t.IsCompleted && !t.IsArchived)
            .ToListAsync();

        foreach (var item in staleCompleted)
        {
            item.IsArchived = true;
            item.ArchivedAt = item.CreatedAt;
            await _db.UpdateAsync(item);
        }
    }

    /// <inheritdoc />
    public async Task<List<TaskItem>> GetAllActiveTasksAsync()
    {
        await InitializeAsync();
        return await _db.Table<TaskItem>()
                  .Where(t => !t.IsDeleted && !t.IsArchived)
                  .OrderByDescending(t => t.Priority)
                  .ThenBy(t => t.DueDate)
                  .ToListAsync();
    }

    /// <summary>
    /// 载入「今日聚焦」任务：到期日为今天**或已逾期**的未完成任务。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么包含逾期</b>：若严格只取今天，昨天到期而未完成的任务会静默消失 ——
    /// 既不在「今日」也不显眼于「全部」，成为注意力盲区。
    /// 这与该视图「今天需要优先解决的关键事项」的定位直接矛盾（design-domain-contract §5.4）。
    /// </para>
    /// <para>
    /// <b>修正的原始缺陷</b>：原实现为
    /// <c>startOfDay = DateTime.Today.ToUniversalTime(); endOfDay = startOfDay.AddDays(1)</c>，
    /// 存在两处错误：
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///   把「日历日」当「瞬间」处理。<c>DueDate</c> 表达的是用户日历上的某一天，
    ///   将其转为 UTC 时刻再加 24 小时，得到的区间在非 UTC 时区下与用户心中的「今天」错位。
    ///   </description></item>
    ///   <item><description>
    ///   直接读取 <c>DateTime.Today</c>，业务分支依赖系统时钟，违反 Article 9，
    ///   且使区间边界无法被测试。
    ///   </description></item>
    /// </list>
    /// <para>
    /// 修正方式：<c>DueDate</c> 以本地日历日语义存取（当日零点、Kind 为 Unspecified），
    /// 因此与 <see cref="IClock.Today"/> <b>直接比较即可，全程不做任何时区换算</b>。
    /// 不换算正是正确性的保证 —— 任何换算都会破坏「哪一天」这一语义。
    /// </para>
    /// </remarks>
    public async Task<List<TaskItem>> GetTodayTasksAsync()
    {
        await InitializeAsync();

        // 取「明天零点」作为开区间上界，等价于「到期日 <= 今天」，天然涵盖逾期项
        var tomorrow = _clock.Today.AddDays(1);

        return await _db.Table<TaskItem>()
                  .Where(t => !t.IsDeleted
                              && !t.IsCompleted
                              && t.DueDate != null
                              && t.DueDate < tomorrow)
                  .OrderBy(t => t.DueDate)
                  .ThenByDescending(t => t.Priority)
                  .ToListAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 语义已随 spec-task-complete-before-archive 从「已完成」改为「已归档」：
    /// 完成但未归档的任务不在此列，仍留在活动列表。
    /// </remarks>
    public async Task<List<TaskItem>> GetCompletedTasksAsync()
    {
        await InitializeAsync();
        return await _db.Table<TaskItem>()
                  .Where(t => !t.IsDeleted && t.IsArchived)
                  .OrderByDescending(t => t.ArchivedAt)
                  .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<TaskItem>> GetTasksByProjectAsync(string? projectId)
    {
        await InitializeAsync();

        // 分成两条查询：sqlite-net 的表达式翻译对「参数为 null 时改变比较语义」
        // 支持不可靠，显式分支比依赖其推断更稳妥
        var query = projectId is null
            ? _db.Table<TaskItem>().Where(t => !t.IsDeleted && !t.IsArchived && t.ProjectId == null)
            : _db.Table<TaskItem>().Where(t => !t.IsDeleted && !t.IsArchived && t.ProjectId == projectId);

        return await query
                  .OrderByDescending(t => t.Priority)
                  .ThenBy(t => t.DueDate)
                  .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<TaskItem?> GetByIdAsync(string id)
    {
        await InitializeAsync();
        return await _db.Table<TaskItem>()
                        .Where(t => t.Id == id)
                        .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 本方法是所有任务写入的唯一漏斗，因此在此统一维护两条不变量，
    /// 而非依赖每个调用方各自遵守（Article 6）：
    /// <list type="number">
    ///   <item><description>
    ///   <c>CreatedAt</c> 必须已被显式赋值。实体默认值为 <c>default</c>，
    ///   若调用方遗漏赋值则快速失败，而非静默写入零值时间。
    ///   </description></item>
    ///   <item><description>
    ///   <c>DueDate</c> 归一化为当日零点。它是「日历日」，
    ///   携带时间部分会使「今日」区间比较出现边界歧义。
    ///   </description></item>
    /// </list>
    /// </remarks>
    public async Task<int> SaveTaskAsync(TaskItem item)
    {
        await InitializeAsync();

        if (item.CreatedAt == default)
        {
            throw new InvalidOperationException(
                $"任务 {item.Id} 的 CreatedAt 未被赋值。创建方必须经 IClock 显式设置创建时间，" +
                "以免业务时间戳依赖实体字段初始化器这一隐式副作用。");
        }

        // 标题校验在此兜底：仓储是唯一写入漏斗，
        // 比依赖每个 UI 入口各自校验更可靠（Article 6）
        var titleError = TaskTitle.Validate(item.Title);
        if (titleError is not null)
        {
            throw new InvalidOperationException($"任务 {item.Id} 标题非法：{titleError}");
        }

        item.Title = TaskTitle.Normalize(item.Title);
        item.DueDate = NormalizeDueDate(item.DueDate);

        var existing = await GetByIdAsync(item.Id);
        if (existing == null)
        {
            return await _db.InsertAsync(item);
        }

        return await _db.UpdateAsync(item);
    }

    /// <summary>
    /// 将到期日归一化为「日历日」：剥离时间部分，Kind 统一为 Unspecified。
    /// </summary>
    /// <remarks>
    /// Kind 一并归一是必要的：<c>sqlite-net</c> 默认以 Ticks 存储 <c>DateTime</c>，
    /// 不保留 Kind。若写入 Local 而读出后被当作 Unspecified 参与比较，
    /// 同一值在写读两侧的语义就不一致。此处提前收敛，使存取对称。
    /// </remarks>
    private static DateTime? NormalizeDueDate(DateTime? dueDate)
        => dueDate is null
            ? null
            : DateTime.SpecifyKind(dueDate.Value.Date, DateTimeKind.Unspecified);

    /// <inheritdoc />
    public async Task<int> SoftDeleteAsync(string id)
    {
        await InitializeAsync();
        var existing = await GetByIdAsync(id);
        if (existing == null) return 0;

        existing.IsDeleted = true;
        return await _db.UpdateAsync(existing);
    }

    /// <inheritdoc />
    public async Task<int> PermanentDeleteAsync(string id)
    {
        await InitializeAsync();
        return await _db.DeleteAsync<TaskItem>(id);
    }

    /// <inheritdoc />
    /// <remarks>
    /// D1：只处理 <c>IsCompleted &amp;&amp; !IsArchived</c> 的行，未完成任务不受影响。
    /// D3：全局范围，不按单项目拆分；归档保留 <c>ProjectId</c> 来源（字段本身不变）。
    /// </remarks>
    public async Task<int> ArchiveAllCompletedAsync()
    {
        await InitializeAsync();

        var pending = await _db.Table<TaskItem>()
            .Where(t => !t.IsDeleted && t.IsCompleted && !t.IsArchived)
            .ToListAsync();

        var archivedAt = _clock.UtcNow;
        foreach (var item in pending)
        {
            item.IsArchived = true;
            item.ArchivedAt = archivedAt;
            await _db.UpdateAsync(item);
        }

        return pending.Count;
    }
}
