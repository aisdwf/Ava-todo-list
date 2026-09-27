namespace FlowTask.Infrastructure.Persistence;

/// <summary>
/// SQLite 文件路径的唯一真源（Article 6）。三个仓储不得各自拼接 LocalAppData。
/// </summary>
public static class DatabaseLocation
{
    /// <summary>显式覆盖整条路径，优先于预览隔离。</summary>
    public const string PathEnvironmentVariable = "FLOWTASK_DB_PATH";

    /// <summary>设为 <c>1</c> 时使用 <c>flowtask.preview.db</c>，避免预览写入日常库。</summary>
    public const string PreviewEnvironmentVariable = "FLOWTASK_PREVIEW";

    /// <summary>应用数据根目录：<c>%LOCALAPPDATA%\FlowTask</c>。</summary>
    public static string RootDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlowTask");

    /// <summary>
    /// 解析最终 db 路径并确保父目录存在。
    /// 优先级：显式参数 → <see cref="PathEnvironmentVariable"/> → 默认文件名。
    /// </summary>
    public static string Resolve(string? explicitPath = null)
    {
        var path = FirstNonEmpty(
                       explicitPath,
                       Environment.GetEnvironmentVariable(PathEnvironmentVariable))
                   ?? Path.Combine(RootDirectory, FileName);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return path;
    }

    /// <summary>日常库 <c>flowtask.db</c>；预览 / <c>dotnet run</c> 用 <c>flowtask.preview.db</c>。</summary>
    public static string FileName =>
        IsolatesFromDailyDatabase() ? "flowtask.preview.db" : "flowtask.db";

    /// <summary>
    /// 预览 exe 与本地 <c>bin/Debug|Release</c> 不得写用户日常库。
    /// </summary>
    public static bool IsolatesFromDailyDatabase()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable(PreviewEnvironmentVariable),
                "1",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var baseDir = AppContext.BaseDirectory;
        return HasDirectorySegment(baseDir, "preview")
               || (HasDirectorySegment(baseDir, "bin")
                   && (HasDirectorySegment(baseDir, "Debug")
                       || HasDirectorySegment(baseDir, "Release")));
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool HasDirectorySegment(string path, string segment)
    {
        var parts = path.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
    }
}
