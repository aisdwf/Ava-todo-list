namespace FlowTask.Desktop.Services;

/// <summary>
/// 单实例策略：拿不到锁则失败退出；强杀与管道替换都按可执行文件路径授权。
/// </summary>
public static class SingleInstancePolicy
{
    public const string ReplaceCommand = "replace";

    public const string FailureMessage =
        "FlowTask 已在运行且无法接管。请先退出已有窗口后再打开。";

    /// <summary>未持有互斥则必须退出，不得再开第二个写入同一库的进程。</summary>
    public static bool MustExit(bool ownsMutex) => !ownsMutex;

    /// <summary>
    /// 仅当对端与本进程是同一可执行文件时才允许强杀，避免误杀其它 preview 目录里的副本。
    /// </summary>
    public static bool CanKillProcess(string? selfExecutablePath, string? otherExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(selfExecutablePath) || string.IsNullOrWhiteSpace(otherExecutablePath))
        {
            return false;
        }

        string self;
        string other;
        try
        {
            self = Path.GetFullPath(selfExecutablePath);
            other = Path.GetFullPath(otherExecutablePath);
        }
        catch (Exception)
        {
            return false;
        }

        return string.Equals(self, other, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>后来者发给旧进程的替换行：命令 + 自身路径。</summary>
    public static string FormatReplaceLine(string requesterExecutablePath)
        => $"{ReplaceCommand}|{requesterExecutablePath}";

    /// <summary>
    /// 管道行必须是 replace，且客户端路径与本进程路径相同。
    /// 任意本地程序只发 <c>replace</c> 不再被接受。
    /// </summary>
    public static bool IsAuthorizedReplace(string? line, string? ownerExecutablePath, string? clientExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(ownerExecutablePath))
        {
            return false;
        }

        var separator = line.IndexOf('|');
        var command = separator < 0 ? line : line[..separator];
        if (!string.Equals(command.Trim(), ReplaceCommand, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var claimedPath = separator < 0 ? null : line[(separator + 1)..];
        return CanKillProcess(ownerExecutablePath, clientExecutablePath)
               && CanKillProcess(ownerExecutablePath, claimedPath);
    }
}
