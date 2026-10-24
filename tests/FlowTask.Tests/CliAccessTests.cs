using System.Text.Json;
using FlowTask.Cli;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;
using FlowTask.Desktop.ViewModels;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

public class CliAccessPolicyTests
{
    [Theory]
    [InlineData(null, false, false)]
    [InlineData("", true, true)]
    [InlineData("nope", false, false)]
    [InlineData("true", false, true)]
    [InlineData("FALSE", true, false)]
    public void ReadBool_UsesFallbackWhenUnsetOrUnknown(string? raw, bool fallback, bool expected)
        => Assert.Equal(expected, CliAccessPolicy.ReadBool(raw, fallback));

    [Fact]
    public void Permissions_RequireTheMasterSwitch()
    {
        var open = new CliAccessPolicy(true, true, true, true);
        var closed = open with { Enabled = false };

        Assert.True(open.ReadPermitted);
        Assert.True(open.WritePermitted);
        Assert.False(closed.ReadPermitted);
        Assert.False(closed.WritePermitted);
        Assert.False(CliAccessPolicy.Default.Enabled);
        Assert.False(CliAccessPolicy.Default.AllowRead);
        Assert.False(CliAccessPolicy.Default.AllowWrite);
        Assert.True(CliAccessPolicy.Default.MarkWrites);
    }
}

public class CliAccessSettingsViewModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteAppSettingsRepository _settings;

    public CliAccessSettingsViewModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_cli_settings_{Guid.NewGuid():N}.db");
        _settings = new SqliteAppSettingsRepository(_dbPath);
    }

    [Fact]
    public async Task Load_UsesDefaultsWhenUnset()
    {
        var vm = new CliAccessSettingsViewModel(_settings);
        await vm.LoadAsync();

        Assert.False(vm.Enabled);
        Assert.False(vm.AllowRead);
        Assert.False(vm.AllowWrite);
        Assert.True(vm.MarkWrites);
        Assert.False(vm.CanConfigurePermissions);
    }

    [Fact]
    public async Task Toggle_RoundTripsAndKeepsReadWriteWhenMasterCloses()
    {
        var vm = new CliAccessSettingsViewModel(_settings);
        await vm.LoadAsync();

        vm.Enabled = true;
        await vm.LastPersist;
        vm.AllowRead = true;
        await vm.LastPersist;
        vm.AllowWrite = true;
        await vm.LastPersist;
        vm.Enabled = false;
        await vm.LastPersist;

        var again = new CliAccessSettingsViewModel(_settings);
        await again.LoadAsync();

        Assert.False(again.Enabled);
        Assert.True(again.AllowRead);
        Assert.True(again.AllowWrite);
        Assert.False(again.CanConfigurePermissions);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
        }
    }
}

public class CliAppTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FixedClock _clock = new();
    private readonly SqliteTaskRepository _tasks;
    private readonly SqliteProjectRepository _projects;
    private readonly SqliteAppSettingsRepository _settings;
    private int _notifies;

    public CliAppTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_cli_{Guid.NewGuid():N}.db");
        _tasks = new SqliteTaskRepository(_clock, _dbPath);
        _projects = new SqliteProjectRepository(_dbPath);
        _settings = new SqliteAppSettingsRepository(_dbPath);
    }

    [Fact]
    public async Task ClosedMaster_RefusesListAndAddWithoutWriting()
    {
        await _settings.SetAsync(CliAccessPolicy.AllowWriteKey, "true");

        var list = await RunAsync("list");
        var add = await RunAsync("add", "--title", "买牛奶");

        Assert.Equal(CliApp.Denied, list.Code);
        Assert.Equal(CliApp.Denied, add.Code);
        Assert.Contains("未打开", list.Error, StringComparison.Ordinal);
        Assert.Empty(list.Output);
        Assert.Empty(add.Output);
        Assert.Empty(await _tasks.GetAllActiveTasksAsync());
        Assert.Equal(0, _notifies);
    }

    [Fact]
    public async Task ReadWithoutWrite_ListsAndRefusesMutation()
    {
        await OpenAsync(read: true, write: false);
        var item = TaskItemFactory.Create(_clock, "界面里的任务");
        item.ProjectId = DefaultProject.Id;
        await _projects.EnsureDefaultProjectAsync(_clock.UtcNow);
        await _tasks.SaveTaskAsync(item);

        var list = await RunAsync("list");
        var add = await RunAsync("add", "--title", "不该写入");
        var delete = await RunAsync("delete", "--id", item.Id);

        Assert.Equal(CliApp.Ok, list.Code);
        Assert.Equal(CliApp.Denied, add.Code);
        Assert.Equal(CliApp.Denied, delete.Code);
        Assert.Contains(item.Id, list.Output, StringComparison.Ordinal);
        Assert.Single(await _tasks.GetAllActiveTasksAsync());
        Assert.Equal(0, _notifies);
    }

    [Fact]
    public async Task List_IncludesTasksUnderArchivedProjects()
    {
        await OpenAsync(read: true, write: false);
        var project = new Project
        {
            Name = "旧项目",
            SortOrder = 1,
            CreatedAt = _clock.UtcNow
        };
        await _projects.SaveProjectAsync(project);
        var item = TaskItemFactory.Create(_clock, "归档里的任务", projectId: project.Id);
        await _tasks.SaveTaskAsync(item);
        await _projects.SetArchivedAsync(project.Id, true);

        var list = await RunAsync("list");

        Assert.Equal(CliApp.Ok, list.Code);
        Assert.Contains(item.Id, list.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(await _tasks.GetAllActiveTasksAsync(), t => t.Id == item.Id);
    }

    [Fact]
    public async Task WriteWithoutRead_AddsMarkedTaskAndRefusesList()
    {
        await OpenAsync(read: false, write: true, mark: true);

        var add = await RunAsync("add", "--title", "买牛奶");
        var list = await RunAsync("list");

        Assert.Equal(CliApp.Ok, add.Code);
        Assert.Equal(CliApp.Denied, list.Code);
        Assert.Empty(list.Output);
        var stored = Assert.Single(await _tasks.GetAllActiveTasksAsync());
        Assert.Equal("买牛奶", stored.Title);
        Assert.Equal(DefaultProject.Id, stored.ProjectId);
        Assert.True(stored.IsCliWritten);
        Assert.Null(stored.DueDate);
        Assert.Equal(1, _notifies);
    }

    [Fact]
    public async Task Add_AcceptsProjectPriorityAndDue_AndCanOmitTheMark()
    {
        await OpenAsync(read: true, write: true, mark: false);
        await _projects.SaveProjectAsync(new Project
        {
            Name = "读书",
            SortOrder = 1,
            CreatedAt = _clock.UtcNow
        });

        var add = await RunAsync(
            "add",
            "--title", "读完一章",
            "--project", "读书",
            "--priority", "High",
            "--due", "2026-11-02");

        Assert.Equal(CliApp.Ok, add.Code);
        using var json = JsonDocument.Parse(add.Output);
        Assert.False(json.RootElement.GetProperty("cli").GetBoolean());
        Assert.Equal("读书", json.RootElement.GetProperty("project").GetString());
        Assert.Equal("High", json.RootElement.GetProperty("priority").GetString());
        Assert.Equal("2026-11-02", json.RootElement.GetProperty("due").GetString());

        var stored = await _tasks.GetByIdAsync(json.RootElement.GetProperty("id").GetString()!);
        Assert.NotNull(stored);
        Assert.False(stored.IsCliWritten);
        Assert.Equal(new DateTime(2026, 11, 2), stored.DueDate);
    }

    [Fact]
    public async Task Add_UnknownProjectOrBlankTitle_DoesNotWrite()
    {
        await OpenAsync(read: false, write: true);

        var missing = await RunAsync("add", "--title", "x", "--project", "没有");
        var blank = await RunAsync("add", "--title", "  ");

        Assert.Equal(CliApp.Rejected, missing.Code);
        Assert.Equal(CliApp.Rejected, blank.Code);
        Assert.Empty(await _tasks.GetAllActiveTasksAsync());
        Assert.Equal(0, _notifies);
    }

    [Fact]
    public async Task Delete_RemovesById_EvenWhenTheTaskWasNotMarked()
    {
        await OpenAsync(read: false, write: true);
        await _projects.EnsureDefaultProjectAsync(_clock.UtcNow);
        var item = TaskItemFactory.Create(_clock, "自己记的", projectId: DefaultProject.Id);
        await _tasks.SaveTaskAsync(item);

        var missing = await RunAsync("delete", "--id", "missing");
        var deleted = await RunAsync("delete", "--id", item.Id);

        Assert.Equal(CliApp.Rejected, missing.Code);
        Assert.Equal(CliApp.Ok, deleted.Code);
        Assert.Null(await _tasks.GetByIdAsync(item.Id));
        Assert.Equal(1, _notifies);
    }

    private async Task OpenAsync(bool read, bool write, bool mark = true)
    {
        await _settings.SetAsync(CliAccessPolicy.EnabledKey, "true");
        await _settings.SetAsync(CliAccessPolicy.AllowReadKey, CliAccessPolicy.StoreBool(read));
        await _settings.SetAsync(CliAccessPolicy.AllowWriteKey, CliAccessPolicy.StoreBool(write));
        await _settings.SetAsync(CliAccessPolicy.MarkWritesKey, CliAccessPolicy.StoreBool(mark));
    }

    private async Task<(int Code, string Output, string Error)> RunAsync(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var app = new CliApp(_tasks, _projects, _settings, _clock, () =>
        {
            _notifies++;
            return Task.CompletedTask;
        });
        var code = await app.RunAsync(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 24, 1, 0, 0, DateTimeKind.Utc);

        public DateTime Today => new(2026, 10, 24);
    }
}
