using FlowTask.Core.Models;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

public class SqliteAppSettingsRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteAppSettingsRepository _settings;

    public SqliteAppSettingsRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_settings_{Guid.NewGuid():N}.db");
        _settings = new SqliteAppSettingsRepository(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task GetDefaultDueOffset_ReturnsOneWhenUnset()
    {
        Assert.Equal(DueDateOffset.DefaultDays, await _settings.GetDefaultDueOffsetDaysAsync());
    }

    [Fact]
    public async Task SetAndGet_RoundTripsValidOffset()
    {
        await _settings.SetDefaultDueOffsetDaysAsync(5);
        Assert.Equal(5, await _settings.GetDefaultDueOffsetDaysAsync());
    }

    [Fact]
    public async Task Set_RejectsOutOfRange()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _settings.SetDefaultDueOffsetDaysAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _settings.SetDefaultDueOffsetDaysAsync(31));
    }

    [Fact]
    public async Task Get_ClampsCorruptStoredValueToDefault()
    {
        await _settings.SetAsync(DueDateOffset.SettingsKey, "999");
        Assert.Equal(DueDateOffset.DefaultDays, await _settings.GetDefaultDueOffsetDaysAsync());
    }
}
