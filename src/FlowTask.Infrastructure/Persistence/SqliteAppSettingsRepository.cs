using SQLite;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Infrastructure.Persistence;

/// <summary>
/// 基于 SQLite 的应用设置仓储。
/// </summary>
public sealed class SqliteAppSettingsRepository : IAppSettingsRepository
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SqliteAppSettingsRepository(string? dbPath = null)
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

            await _db.CreateTableAsync<AppSetting>();
            _initialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key)
    {
        await InitializeAsync();
        var row = await _db.FindAsync<AppSetting>(key);
        return row?.Value;
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string value)
    {
        await InitializeAsync();
        await _db.InsertOrReplaceAsync(new AppSetting { Key = key, Value = value });
    }

    /// <inheritdoc />
    public async Task<int> GetDefaultDueOffsetDaysAsync()
    {
        var raw = await GetAsync(DueDateOffset.SettingsKey);
        if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw, out var days))
        {
            return DueDateOffset.DefaultDays;
        }

        return DueDateOffset.ClampOrDefault(days);
    }

    /// <inheritdoc />
    public async Task SetDefaultDueOffsetDaysAsync(int days)
    {
        if (!DueDateOffset.IsValid(days))
        {
            throw new ArgumentOutOfRangeException(
                nameof(days),
                DueDateOffset.Validate(days));
        }

        await SetAsync(DueDateOffset.SettingsKey, days.ToString());
    }
}
