using System.Text.Json;
using FlowTask.Cli;
using Xunit;
using FlowTask.Core.Enums;
using FlowTask.Core.Models;
using FlowTask.Desktop.Services;
using FlowTask.Desktop.ViewModels;
using FlowTask.Desktop.ViewModels.Actions;
using FlowTask.Infrastructure.Export;
using FlowTask.Infrastructure.Persistence;

namespace FlowTask.Tests;

/// <summary>
/// 导出快照的字段、排除项和日期格式。
/// </summary>
public class JsonDataExporterTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_export_{Guid.NewGuid():N}.db");
    private readonly FakeClock _clock = new(
        new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 25));

    public void Dispose()
    {
        TryDelete(_dbPath);
    }

    [Fact]
    public async Task Write_EmitsContractFieldsAndOmitsSettingsDisplayTextAndDeletedRows()
    {
        var projects = new SqliteProjectRepository(_dbPath);
        var tasks = new SqliteTaskRepository(_clock, _dbPath);
        var settings = new SqliteAppSettingsRepository(_dbPath);
        await projects.EnsureDefaultProjectAsync(_clock.UtcNow);

        var archived = new Project
        {
            Id = "project-archived",
            Name = "已归档",
            SortOrder = 2,
            IsArchived = true,
            CreatedAt = new DateTime(2026, 10, 2, 3, 4, 5, DateTimeKind.Utc)
        };
        await projects.SaveProjectAsync(archived);

        var kept = new TaskItem
        {
            Id = "task-kept",
            Title = "工作里的一件事",
            ProjectId = DefaultProject.Id,
            Priority = TaskPriority.High,
            DueDate = new DateTime(2026, 10, 3, 15, 30, 0, DateTimeKind.Utc),
            IsCompleted = true,
            CompletedAt = new DateTime(2026, 10, 3, 4, 5, 6, DateTimeKind.Utc),
            IsCliWritten = true,
            IsPinned = true,
            PinnedAt = new DateTime(2026, 10, 4, 5, 6, 7, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 10, 1, 1, 2, 3, DateTimeKind.Utc),
            Description = "不要出现在文件里",
            IsArchived = true,
            ArchivedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var inArchive = new TaskItem
        {
            Id = "task-archived-project",
            Title = "归档项目里的任务",
            ProjectId = archived.Id,
            Priority = TaskPriority.Low,
            CreatedAt = _clock.UtcNow
        };
        var removed = new TaskItem
        {
            Id = "task-deleted",
            Title = "软删历史行",
            ProjectId = DefaultProject.Id,
            IsDeleted = true,
            CreatedAt = _clock.UtcNow,
            Description = "软删说明也不该出现"
        };
        await tasks.SaveTaskAsync(kept);
        await tasks.SaveTaskAsync(inArchive);
        await tasks.SaveTaskAsync(removed);

        await settings.SetAsync(CliAccessPolicy.EnabledKey, "true");
        await settings.SetAsync(CliAccessPolicy.AllowReadKey, "true");

        using var buffer = new MemoryStream();
        var written = await new JsonDataExporter(projects, tasks, _clock).WriteAsync(buffer);
        var json = System.Text.Encoding.UTF8.GetString(buffer.ToArray());

        Assert.Equal(2, written.ProjectCount);
        Assert.Equal(2, written.TaskCount);
        Assert.Contains("工作里的一件事", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u5de5", json, StringComparison.Ordinal);
        Assert.Contains("\n  \"schemaVersion\"", json, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(
            new[] { "schemaVersion", "exportedAt", "projects", "tasks" },
            Names(root));
        Assert.Equal(ExportSchema.Current, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("2026-10-24T08:00:00Z", root.GetProperty("exportedAt").GetString());

        var projectRows = root.GetProperty("projects").EnumerateArray().ToList();
        Assert.Equal(2, projectRows.Count);
        Assert.Contains(projectRows, row => row.GetProperty("id").GetString() == DefaultProject.Id);
        var archivedRow = Assert.Single(projectRows, row => row.GetProperty("id").GetString() == archived.Id);
        Assert.Equal(
            new[] { "id", "name", "sortOrder", "isArchived", "createdAt" },
            Names(archivedRow));
        Assert.Equal("已归档", archivedRow.GetProperty("name").GetString());
        Assert.True(archivedRow.GetProperty("isArchived").GetBoolean());
        Assert.Equal("2026-10-02T03:04:05Z", archivedRow.GetProperty("createdAt").GetString());

        var taskRows = root.GetProperty("tasks").EnumerateArray().ToList();
        Assert.Equal(2, taskRows.Count);
        Assert.DoesNotContain(taskRows, row => row.GetProperty("id").GetString() == removed.Id);
        var keptRow = Assert.Single(taskRows, row => row.GetProperty("id").GetString() == kept.Id);
        Assert.Equal(
            new[]
            {
                "id", "projectId", "title", "priority", "dueDate", "isCompleted",
                "completedAt", "isCliWritten", "isPinned", "pinnedAt", "createdAt"
            },
            Names(keptRow));
        Assert.Equal("High", keptRow.GetProperty("priority").GetString());
        Assert.Equal("2026-10-03", keptRow.GetProperty("dueDate").GetString());
        Assert.True(keptRow.GetProperty("isCompleted").GetBoolean());
        Assert.Equal("2026-10-03T04:05:06Z", keptRow.GetProperty("completedAt").GetString());
        Assert.True(keptRow.GetProperty("isCliWritten").GetBoolean());
        Assert.True(keptRow.GetProperty("isPinned").GetBoolean());
        Assert.Equal("2026-10-04T05:06:07Z", keptRow.GetProperty("pinnedAt").GetString());
        Assert.Equal("2026-10-01T01:02:03Z", keptRow.GetProperty("createdAt").GetString());
        Assert.Contains(taskRows, row => row.GetProperty("id").GetString() == inArchive.Id);

        Assert.DoesNotContain("不要出现在文件里", json, StringComparison.Ordinal);
        Assert.DoesNotContain("软删说明也不该出现", json, StringComparison.Ordinal);
        Assert.DoesNotContain("已完成", json, StringComparison.Ordinal);
        Assert.DoesNotContain("colorHex", json, StringComparison.Ordinal);
        Assert.DoesNotContain(CliAccessPolicy.EnabledKey, json, StringComparison.Ordinal);
        Assert.DoesNotContain(CliAccessPolicy.AllowReadKey, json, StringComparison.Ordinal);
        Assert.DoesNotContain(CliAccessPolicy.AllowWriteKey, json, StringComparison.Ordinal);
        Assert.DoesNotContain(CliAccessPolicy.MarkWritesKey, json, StringComparison.Ordinal);

        var list = await RunListAsync(tasks, projects, settings);
        using var listDocument = JsonDocument.Parse(list);
        Assert.Equal(JsonValueKind.Array, listDocument.RootElement.ValueKind);
        var listRow = Assert.Single(
            listDocument.RootElement.EnumerateArray().ToList(),
            row => row.GetProperty("id").GetString() == kept.Id);
        Assert.Equal(
            new[] { "id", "title", "project", "priority", "due", "completed", "cli" },
            Names(listRow));
        Assert.True(listRow.GetProperty("cli").GetBoolean());
        Assert.Equal(DefaultProject.Name, listRow.GetProperty("project").GetString());
    }

    private async Task<string> RunListAsync(
        SqliteTaskRepository tasks,
        SqliteProjectRepository projects,
        SqliteAppSettingsRepository settings)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await new CliApp(tasks, projects, settings, _clock).RunAsync(["list"], stdout, stderr);
        Assert.Equal(CliApp.Ok, code);
        Assert.Empty(stderr.ToString());
        return stdout.ToString();
    }

    private static string[] Names(JsonElement element)
        => element.EnumerateObject().Select(property => property.Name).ToArray();

    private static void TryDelete(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>
/// 导出动作：默认文件名、取消不提示、失败不留半截文件。
/// </summary>
public class ExportDataViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"flowtask_export_ui_{Guid.NewGuid():N}");
    private readonly string _dbPath;
    private readonly FakeClock _clock = new(
        new DateTime(2026, 10, 24, 16, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 25));

    public ExportDataViewModelTests()
    {
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "flowtask.db");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Export_UsesLocalDateInTheFileNameAndReportsCounts()
    {
        var vm = CreateViewModel();
        var path = Path.Combine(_root, "out.json");
        await File.WriteAllTextAsync(path, "NOT-JSON");
        var picker = new ScriptedPicker(_ => new ExportSaveChoice { LocalPath = path });
        vm.SavePicker = picker;

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal("flowtask-export-20261025.json", picker.SuggestedFileName);
        Assert.Equal("已导出 1 个项目、0 条任务", vm.ResultText);
        var text = await File.ReadAllTextAsync(path);
        Assert.StartsWith("{", text.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("NOT-JSON", text, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_root, "*.flowtask-export"));
    }

    [Fact]
    public async Task Cancel_LeavesThePreviousResultUntouched()
    {
        var vm = CreateViewModel();
        var path = Path.Combine(_root, "out.json");
        var picker = new ScriptedPicker(_ => new ExportSaveChoice { LocalPath = path });
        vm.SavePicker = picker;
        await vm.ExportCommand.ExecuteAsync(null);

        picker.Next = _ => null;
        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal("已导出 1 个项目、0 条任务", vm.ResultText);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task FailedReplace_DeletesTheStagingFileAndKeepsThePreviousBackup()
    {
        var vm = CreateViewModel();
        var path = Path.Combine(_root, "out.json");
        var picker = new ScriptedPicker(_ => new ExportSaveChoice { LocalPath = path });
        vm.SavePicker = picker;
        await vm.ExportCommand.ExecuteAsync(null);
        var backup = await File.ReadAllTextAsync(path);

        var blocked = Path.Combine(_root, "blocked");
        Directory.CreateDirectory(blocked);
        picker.Next = _ => new ExportSaveChoice { LocalPath = blocked };
        await vm.ExportCommand.ExecuteAsync(null);

        Assert.DoesNotContain("已导出", vm.ResultText, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(vm.ResultText));
        Assert.Equal(backup, await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.GetFiles(_root, "*.flowtask-export"));
        Assert.Empty(Directory.GetFiles(blocked));
    }

    [Fact]
    public async Task MissingPicker_ReportsWhyAndWritesNothing()
    {
        var vm = CreateViewModel();

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal("无法打开保存对话框。", vm.ResultText);
        Assert.Empty(Directory.GetFiles(_root, "*.json"));
    }

    [Fact]
    public void DataSection_SitsAfterCliAndBeforeGuide()
    {
        var vm = new MainViewModel(
            new SqliteTaskRepository(_clock, _dbPath),
            new SqliteProjectRepository(_dbPath),
            _clock,
            new SqliteAppSettingsRepository(_dbPath));
        var sections = vm.SettingsNavItems.Select(item => item.Section).ToList();

        Assert.Equal(sections.IndexOf(SettingsSection.Cli) + 1, sections.IndexOf(SettingsSection.Data));
        Assert.Equal(sections.IndexOf(SettingsSection.Data) + 1, sections.IndexOf(SettingsSection.Archived));
        Assert.Equal(sections.IndexOf(SettingsSection.Archived) + 1, sections.IndexOf(SettingsSection.Guide));
        Assert.Equal("数据", vm.SettingsNavItems.Single(item => item.Section == SettingsSection.Data).DisplayName);
    }

    private ExportDataViewModel CreateViewModel()
    {
        var projects = new SqliteProjectRepository(_dbPath);
        var tasks = new SqliteTaskRepository(_clock, _dbPath);
        projects.EnsureDefaultProjectAsync(_clock.UtcNow).GetAwaiter().GetResult();
        return new ExportDataViewModel(new JsonDataExporter(projects, tasks, _clock), _clock);
    }

    private sealed class ScriptedPicker : IExportSavePicker
    {
        public ScriptedPicker(Func<string, ExportSaveChoice?> next)
        {
            Next = next;
        }

        public string? SuggestedFileName { get; private set; }

        public Func<string, ExportSaveChoice?> Next { get; set; }

        public Task<ExportSaveChoice?> PickAsync(string suggestedFileName)
        {
            SuggestedFileName = suggestedFileName;
            return Task.FromResult(Next(suggestedFileName));
        }
    }
}
