using Avalonia.Headless.XUnit;
using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;
using FlowTask.Desktop.ViewModels;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 覆盖启动失败日志与 fire-and-forget 不得吞异常（扫描 H3）。
/// </summary>
public class AppLogTests : IDisposable
{
    private readonly string _logDir;
    private readonly string _previousDir;

    public AppLogTests()
    {
        _previousDir = AppLog.DirectoryPath;
        _logDir = Path.Combine(Path.GetTempPath(), $"flowtask_logs_{Guid.NewGuid():N}");
        AppLog.DirectoryPath = _logDir;
    }

    public void Dispose()
    {
        AppLog.DirectoryPath = _previousDir;
        try
        {
            if (Directory.Exists(_logDir))
            {
                Directory.Delete(_logDir, recursive: true);
            }
        }
        catch
        {
            // 清理锁不影响断言
        }
    }

    [Fact]
    public void Write_CreatesLogFileWithContextAndException()
    {
        AppLog.Write("disk full", new IOException("no space"));

        var files = Directory.GetFiles(_logDir, "flowtask-*.log");
        var text = File.ReadAllText(Assert.Single(files));

        Assert.Contains("disk full", text, StringComparison.Ordinal);
        Assert.Contains("no space", text, StringComparison.Ordinal);
        Assert.Contains("IOException", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FireAndForget_LogsFaultedTask()
    {
        LoggedTasks.FireAndForget(
            Task.FromException(new InvalidOperationException("boom")),
            "background load");

        var files = Directory.GetFiles(_logDir, "flowtask-*.log");
        var text = File.ReadAllText(Assert.Single(files));

        Assert.Contains("background load", text, StringComparison.Ordinal);
        Assert.Contains("boom", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task InitializeAsync_SetsVisibleErrorAndWritesLogWhenSettingsFail()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_h3_{Guid.NewGuid():N}.db");
        try
        {
            var clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
            var vm = new MainViewModel(
                new SqliteTaskRepository(clock, dbPath),
                new SqliteProjectRepository(dbPath),
                clock,
                new ThrowingSettingsRepository());

            await vm.InitializeAsync();

            Assert.True(vm.HasInitializationError);
            Assert.Contains("无法加载", vm.InitializationError, StringComparison.Ordinal);

            var files = Directory.GetFiles(_logDir, "flowtask-*.log");
            var text = File.ReadAllText(Assert.Single(files));
            Assert.Contains("InitializeAsync", text, StringComparison.Ordinal);
            Assert.Contains("disk full", text, StringComparison.Ordinal);
        }
        finally
        {
            try { File.Delete(dbPath); } catch { /* 清理锁 */ }
        }
    }

    private sealed class ThrowingSettingsRepository : IAppSettingsRepository
    {
        public Task InitializeAsync() => throw new IOException("disk full");

        public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);

        public Task SetAsync(string key, string value) => Task.CompletedTask;

        public Task<int> GetDefaultDueOffsetDaysAsync() => Task.FromResult(0);

        public Task SetDefaultDueOffsetDaysAsync(int days) => Task.CompletedTask;
    }
}
