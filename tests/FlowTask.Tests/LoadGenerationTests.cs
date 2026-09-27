using Avalonia.Headless.XUnit;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Messages;
using FlowTask.Core.Models;
using FlowTask.Desktop.ViewModels;
using FlowTask.Infrastructure.Persistence;
using CommunityToolkit.Mvvm.Messaging;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 快速切换时过期的列表加载必须丢弃（扫描 M1）。
/// </summary>
public class LoadGenerationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FakeClock _clock;
    private readonly SqliteProjectRepository _projects;
    private readonly SqliteAppSettingsRepository _settings;

    public LoadGenerationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_loadgen_{Guid.NewGuid():N}.db");
        _clock = new FakeClock(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        _projects = new SqliteProjectRepository(_dbPath);
        _settings = new SqliteAppSettingsRepository(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* 清理锁 */ }
        }
    }

    [AvaloniaFact]
    public async Task LoadTasksAsync_DiscardsStaleQueryAfterNewerLoad()
    {
        var staleItems = new List<TaskItem> { TaskItemFactory.Create(_clock, "过期结果") };
        var freshItems = new List<TaskItem> { TaskItemFactory.Create(_clock, "新结果") };
        var staleGate = new TaskCompletionSource();
        var tasks = new ScriptedTaskRepository(staleGate, staleItems, freshItems);

        var vm = new MainViewModel(tasks, _projects, _clock, _settings);
        await vm.InitializeAsync();
        Assert.False(vm.HasInitializationError);
        Assert.Empty(vm.Tasks);

        tasks.ArmOverlap();
        var staleLoad = vm.RefreshTasksCommand.ExecuteAsync(null);
        await tasks.StaleStarted.Task;
        WeakReferenceMessenger.Default.Send(new TaskDeletedMessage("stale", this));
        await vm.TaskListRefreshTask;

        Assert.Equal("新结果", Assert.Single(vm.Tasks).Task.Title);

        staleGate.SetResult();
        await staleLoad;

        Assert.Equal("新结果", Assert.Single(vm.Tasks).Task.Title);
    }

    [AvaloniaFact]
    public async Task LoadTasksForSelectedProjectAsync_DiscardsStaleQueryAfterNewerLoad()
    {
        var staleItems = new List<TaskItem> { TaskItemFactory.Create(_clock, "过期结果") };
        var freshItems = new List<TaskItem> { TaskItemFactory.Create(_clock, "新结果") };
        var staleGate = new TaskCompletionSource();
        var tasks = new ScriptedTaskRepository(staleGate, staleItems, freshItems);

        var vm = new QuickCaptureViewModel(tasks, _projects, _settings, _clock);
        await vm.PrepareAsync();
        Assert.Empty(vm.Tasks);

        tasks.ArmOverlap();
        var staleLoad = vm.ChangeSelectedProjectCommand.ExecuteAsync(vm.SelectedProject);
        await tasks.StaleStarted.Task;
        WeakReferenceMessenger.Default.Send(new TaskSavedMessage(TaskItemFactory.Create(_clock, "触发重载"), this));
        await vm.TaskListRefreshTask;

        Assert.Equal("新结果", Assert.Single(vm.Tasks).Task.Title);

        staleGate.SetResult();
        await staleLoad;

        Assert.Equal("新结果", Assert.Single(vm.Tasks).Task.Title);
    }

    private sealed class ScriptedTaskRepository : ITaskRepository
    {
        private readonly TaskCompletionSource _staleGate;
        private readonly List<TaskItem> _staleItems;
        private readonly List<TaskItem> _freshItems;
        private bool _armed;
        private int _armedCalls;

        public TaskCompletionSource StaleStarted { get; } = new();

        public ScriptedTaskRepository(
            TaskCompletionSource staleGate,
            List<TaskItem> staleItems,
            List<TaskItem> freshItems)
        {
            _staleGate = staleGate;
            _staleItems = staleItems;
            _freshItems = freshItems;
        }

        public void ArmOverlap()
        {
            _armedCalls = 0;
            _armed = true;
        }

        public async Task<List<TaskItem>> GetAllActiveTasksAsync()
            => _armed ? await NextQueryAsync() : [];

        public Task<List<TaskItem>> GetTodayTasksAsync() => Task.FromResult(new List<TaskItem>());

        public async Task<List<TaskItem>> GetTasksByProjectAsync(string? projectId)
            => _armed ? await NextQueryAsync() : [];

        private async Task<List<TaskItem>> NextQueryAsync()
        {
            var n = Interlocked.Increment(ref _armedCalls);
            if (n == 1)
            {
                StaleStarted.TrySetResult();
                await _staleGate.Task;
                return _staleItems;
            }

            return _freshItems;
        }

        public Task<TaskItem?> GetByIdAsync(string id) => Task.FromResult<TaskItem?>(null);

        public Task<int> SaveTaskAsync(TaskItem item) => Task.FromResult(0);

        public Task<int> PermanentDeleteAsync(string id) => Task.FromResult(0);
    }
}
