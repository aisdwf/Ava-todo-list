using System.Globalization;
using System.Text.Json;
using FlowTask.Core.Enums;
using FlowTask.Core.Interfaces;
using FlowTask.Core.Models;

namespace FlowTask.Cli;

/// <summary>
/// 命令行的加入、删除与列出。权限与字段规则集中在这里，避免 Program 再写一套。
/// </summary>
public sealed class CliApp
{
    /// <summary>成功。</summary>
    public const int Ok = 0;

    /// <summary>参数、标题、项目或 id 不成立。数据库不变。</summary>
    public const int Rejected = 1;

    /// <summary>总开关或对应的读/写权限关闭。</summary>
    public const int Denied = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ITaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IAppSettingsRepository _settings;
    private readonly IClock _clock;
    private readonly Func<Task> _notify;

    public CliApp(
        ITaskRepository tasks,
        IProjectRepository projects,
        IAppSettingsRepository settings,
        IClock clock,
        Func<Task>? notify = null)
    {
        _tasks = tasks;
        _projects = projects;
        _settings = settings;
        _clock = clock;
        _notify = notify ?? (() => Task.CompletedTask);
    }

    /// <summary>执行一次命令。拒绝时不写 stdout。</summary>
    public async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            await stderr.WriteLineAsync(Usage);
            return Rejected;
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();
        return command switch
        {
            "list" => await ListAsync(rest, stdout, stderr),
            "add" => await AddAsync(rest, stdout, stderr),
            "delete" => await DeleteAsync(rest, stdout, stderr),
            _ => await RejectAsync(stderr, Usage)
        };
    }

    private async Task<int> ListAsync(string[] rest, TextWriter stdout, TextWriter stderr)
    {
        if (rest.Length > 0)
        {
            return await RejectAsync(stderr, Usage);
        }

        var policy = await LoadPolicyAsync();
        if (!policy.Enabled)
        {
            return await DenyAsync(stderr, "命令行交互未打开。");
        }

        if (!policy.AllowRead)
        {
            return await DenyAsync(stderr, "命令行没有读取权限。");
        }

        // list 是显式读取：已归档项目下的任务仍返回。看板过滤在 GetAllActiveTasksAsync。
        var tasks = await _tasks.GetAllNonDeletedTasksAsync();
        var names = await ProjectNamesAsync();
        var rows = tasks.Select(task => ToRow(task, names)).ToList();
        await stdout.WriteLineAsync(JsonSerializer.Serialize(rows, JsonOptions));
        return Ok;
    }

    private async Task<int> AddAsync(string[] rest, TextWriter stdout, TextWriter stderr)
    {
        if (!TryReadOptions(rest, out var options, out var error))
        {
            return await RejectAsync(stderr, error!);
        }

        if (options.Keys.Any(key => key is not ("title" or "project" or "priority" or "due")))
        {
            return await RejectAsync(stderr, Usage);
        }

        var policy = await LoadPolicyAsync();
        if (!policy.Enabled)
        {
            return await DenyAsync(stderr, "命令行交互未打开。");
        }

        if (!policy.AllowWrite)
        {
            return await DenyAsync(stderr, "命令行没有写入权限。");
        }

        if (!options.TryGetValue("title", out var title))
        {
            return await RejectAsync(stderr, "加入任务需要 --title。");
        }

        var titleError = TaskTitle.Validate(title);
        if (titleError is not null)
        {
            return await RejectAsync(stderr, titleError);
        }

        if (!TryPriority(options, out var priority, out error))
        {
            return await RejectAsync(stderr, error!);
        }

        if (!TryDue(options, out var due, out error))
        {
            return await RejectAsync(stderr, error!);
        }

        string projectId;
        if (!options.TryGetValue("project", out var projectName))
        {
            await _projects.EnsureDefaultProjectAsync(_clock.UtcNow);
            projectId = DefaultProject.Id;
        }
        else
        {
            var resolved = await ResolveProjectAsync(projectName);
            if (resolved.Error is not null)
            {
                return await RejectAsync(stderr, resolved.Error);
            }

            projectId = resolved.ProjectId;
        }

        var item = TaskItemFactory.Create(_clock, title, priority, projectId, due);
        item.IsCliWritten = policy.MarkWrites;
        await _tasks.SaveTaskAsync(item);
        await NotifyAsync();

        var names = await ProjectNamesAsync();
        await stdout.WriteLineAsync(JsonSerializer.Serialize(ToRow(item, names), JsonOptions));
        return Ok;
    }

    private async Task<int> DeleteAsync(string[] rest, TextWriter stdout, TextWriter stderr)
    {
        if (!TryReadOptions(rest, out var options, out var error))
        {
            return await RejectAsync(stderr, error!);
        }

        if (options.Keys.Any(key => key != "id") || !options.TryGetValue("id", out var id) || string.IsNullOrWhiteSpace(id))
        {
            return await RejectAsync(stderr, "删除任务需要 --id。");
        }

        var policy = await LoadPolicyAsync();
        if (!policy.Enabled)
        {
            return await DenyAsync(stderr, "命令行交互未打开。");
        }

        if (!policy.AllowWrite)
        {
            return await DenyAsync(stderr, "命令行没有写入权限。");
        }

        var existing = await _tasks.GetByIdAsync(id);
        if (existing is null || existing.IsDeleted)
        {
            return await RejectAsync(stderr, "没有这条任务。");
        }

        await _tasks.PermanentDeleteAsync(id);
        await NotifyAsync();
        await stdout.WriteLineAsync(JsonSerializer.Serialize(new { deleted = id }, JsonOptions));
        return Ok;
    }

    private async Task<CliAccessPolicy> LoadPolicyAsync()
    {
        return new CliAccessPolicy(
            CliAccessPolicy.ReadBool(await _settings.GetAsync(CliAccessPolicy.EnabledKey), CliAccessPolicy.Default.Enabled),
            CliAccessPolicy.ReadBool(await _settings.GetAsync(CliAccessPolicy.AllowReadKey), CliAccessPolicy.Default.AllowRead),
            CliAccessPolicy.ReadBool(await _settings.GetAsync(CliAccessPolicy.AllowWriteKey), CliAccessPolicy.Default.AllowWrite),
            CliAccessPolicy.ReadBool(await _settings.GetAsync(CliAccessPolicy.MarkWritesKey), CliAccessPolicy.Default.MarkWrites));
    }

    private async Task<(string ProjectId, string? Error)> ResolveProjectAsync(string name)
    {
        var normalized = ProjectName.Normalize(name);
        var nameError = ProjectName.Validate(normalized);
        if (nameError is not null)
        {
            return (string.Empty, nameError);
        }

        var match = (await _projects.GetAllProjectsAsync())
            .FirstOrDefault(project => string.Equals(
                ProjectName.Normalize(project.Name),
                normalized,
                StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return (string.Empty, $"没有名为「{normalized}」的项目。");
        }

        return (match.Id, null);
    }

    private async Task<Dictionary<string, string>> ProjectNamesAsync()
        => (await _projects.GetAllProjectsAsync()).ToDictionary(project => project.Id, project => project.Name);

    private static object ToRow(TaskItem task, IReadOnlyDictionary<string, string> names)
    {
        string? project = null;
        if (task.ProjectId is not null)
        {
            project = names.TryGetValue(task.ProjectId, out var name) ? name : task.ProjectId;
        }

        return new
        {
            id = task.Id,
            title = task.Title,
            project,
            priority = task.Priority.ToString(),
            due = task.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            completed = task.IsCompleted,
            cli = task.IsCliWritten
        };
    }

    private static bool TryPriority(IReadOnlyDictionary<string, string> options, out TaskPriority priority, out string? error)
    {
        priority = TaskPriority.Medium;
        error = null;
        if (!options.TryGetValue("priority", out var raw))
        {
            return true;
        }

        if (Enum.TryParse(raw, ignoreCase: true, out priority)
            && string.Equals(priority.ToString(), raw, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        error = "优先级只能是 Low、Medium 或 High。";
        return false;
    }

    private static bool TryDue(IReadOnlyDictionary<string, string> options, out DateTime? due, out string? error)
    {
        due = null;
        error = null;
        if (!options.TryGetValue("due", out var raw))
        {
            return true;
        }

        if (!DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            error = "到期日要用 yyyy-MM-dd。";
            return false;
        }

        due = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Unspecified);
        return true;
    }

    private static bool TryReadOptions(string[] args, out Dictionary<string, string> options, out string? error)
    {
        options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length < 3)
            {
                error = $"无法识别的参数：{token}";
                return false;
            }

            var name = token[2..];
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"参数 --{name} 缺少取值。";
                return false;
            }

            if (!options.TryAdd(name, args[++i]))
            {
                error = $"参数 --{name} 重复。";
                return false;
            }
        }

        error = null;
        return true;
    }

    private async Task NotifyAsync()
    {
        try
        {
            await _notify();
        }
        catch
        {
            // 通知失败不撤销已经落库的写入。
        }
    }

    private static async Task<int> RejectAsync(TextWriter stderr, string message)
    {
        await stderr.WriteLineAsync(message);
        return Rejected;
    }

    private static async Task<int> DenyAsync(TextWriter stderr, string message)
    {
        await stderr.WriteLineAsync(message);
        return Denied;
    }

    private const string Usage =
        """
        用法：
          flowtask list
          flowtask add --title <标题> [--project <项目名>] [--priority Low|Medium|High] [--due yyyy-MM-dd]
          flowtask delete --id <任务id>
        """;
}
