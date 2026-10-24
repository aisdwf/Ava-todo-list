using System.IO.Pipes;

namespace FlowTask.Core.Cli;

/// <summary>
/// 命令行写入成功后通知正在运行的桌面进程。
/// </summary>
/// <remarks>
/// 不复用 <c>FlowTask.SingleInstance.Pipe</c>：那根管道的契约是请旧进程退出。
/// 桌面没在听时通知失败，调用方仍应把数据库里的写入当作成功。
/// </remarks>
public static class CliChangeChannel
{
    /// <summary>本机管道名。桌面监听，命令行在写入成功后连接一次。</summary>
    public const string PipeName = "FlowTask.Cli.Change";

    /// <summary>唯一的消息。桌面收到后重载列表。</summary>
    public const string ChangedLine = "changed";

    /// <summary>尽力通知。没有监听方或超时则安静返回。</summary>
    public static async Task TryNotifyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            await client.ConnectAsync(200, cancellationToken).ConfigureAwait(false);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            await writer.WriteLineAsync(ChangedLine).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
        catch (IOException)
        {
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
