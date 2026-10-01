namespace FlowTask.Desktop.Services;

/// <summary>
/// 将诊断写入本地日志文件。WinExe 没有控制台，失败必须落盘才能事后查看。
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();

    /// <summary>
    /// 日志目录。默认 <c>%LOCALAPPDATA%\FlowTask\logs</c>；测试可改到临时目录。
    /// </summary>
    public static string DirectoryPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlowTask",
        "logs");

    /// <summary>追加一行；附带异常时写入类型与堆栈。</summary>
    public static void Write(string context, Exception? exception = null)
    {
        var stamp = DateTime.UtcNow.ToString("o");
        var line = exception is null
            ? $"{stamp} {context}"
            : $"{stamp} {context}{Environment.NewLine}{exception}";

        lock (Gate)
        {
            Directory.CreateDirectory(DirectoryPath);
            var file = Path.Combine(DirectoryPath, $"flowtask-{DateTime.UtcNow:yyyyMMdd}.log");
            File.AppendAllText(file, line + Environment.NewLine + Environment.NewLine);
        }
    }
}
