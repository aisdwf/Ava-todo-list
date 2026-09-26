using Avalonia.Headless.XUnit;
using FlowTask.Core.Models;
using FlowTask.Desktop.ViewModels;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 主窗与快捷小窗勾选必须从同一 SQLite 源重载列表（spec-cross-window-complete-sync）。
/// </summary>
public class CrossWindowCompleteSyncTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock;
    private readonly SqliteTaskRepository _taskRepo;
    private readonly SqliteProjectRepository _projectRepo;
    private readonly SqliteAppSettingsRepository _settingsRepo;

    public CrossWindowCompleteSyncTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_sync_{Guid.NewGuid():N}.db");
        _clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        _taskRepo = new SqliteTaskRepository(_clock, _dbPath);
        _projectRepo = new SqliteProjectRepository(_dbPath);
        _settingsRepo = new SqliteAppSettingsRepository(_dbPath);
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
                // 测试清理阶段的文件锁不影响断言结果
            }
        }
    }

    private MainViewModel CreateMain()
        => new(_taskRepo, _projectRepo, _clock, _settingsRepo);

    private QuickCaptureViewModel CreateQuick()
        => new(_taskRepo, _projectRepo, _settingsRepo, _clock);

    [AvaloniaFact]
    public async Task QuickToggle_ReloadsMainListFromStore()
    {
        var main = CreateMain();
        var quick = CreateQuick();
        await main.InitializeAsync();

        var task = TaskItemFactory.Create(_clock, "跨窗勾选", projectId: DefaultProject.Id);
        await _taskRepo.SaveTaskAsync(task);
        await main.RefreshTasksCommand.ExecuteAsync(null);
        await quick.PrepareAsync();

        Assert.Single(main.Tasks);
        Assert.Single(quick.Tasks);

        var mainRow = main.Tasks[0];
        var item = quick.Tasks[0].Task;
        item.IsCompleted = true;
        await quick.ToggleTaskCompleteCommand.ExecuteAsync(item);
        await main.TaskListRefreshTask;

        Assert.Same(mainRow, main.Tasks.Single(r => r.Task.Id == mainRow.Task.Id));
        Assert.True(mainRow.IsCompleted);
        Assert.NotNull(mainRow.Task.CompletedAt);
        Assert.True(mainRow.Task.IsCompleted);
    }

    [AvaloniaFact]
    public async Task MainToggle_ReloadsQuickListFromStore()
    {
        var main = CreateMain();
        var quick = CreateQuick();
        await main.InitializeAsync();

        var task = TaskItemFactory.Create(_clock, "跨窗勾选", projectId: DefaultProject.Id);
        await _taskRepo.SaveTaskAsync(task);
        await main.RefreshTasksCommand.ExecuteAsync(null);
        await quick.PrepareAsync();

        var quickRow = quick.Tasks[0];
        var item = main.Tasks[0].Task;
        item.IsCompleted = true;
        await main.ToggleCompleteCommand.ExecuteAsync(item);
        await quick.TaskListRefreshTask;

        Assert.Same(quickRow, quick.Tasks.Single(r => r.Task.Id == quickRow.Task.Id));
        Assert.True(quickRow.IsCompleted);
        Assert.NotNull(quickRow.Task.CompletedAt);
        Assert.True(quickRow.Task.IsCompleted);
    }
}
