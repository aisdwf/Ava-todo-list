using System.IO.Pipes;
using CommunityToolkit.Mvvm.Messaging;
using FlowTask.Core.Cli;
using FlowTask.Core.Messages;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 监听命令行的变更通知，并在本进程内广播 <see cref="CliStoreChangedMessage"/>。
/// </summary>
/// <remarks>
/// 管道只表达「库已变」，不承载任务内容。退出契约仍由 <see cref="SingleInstanceGuard"/> 独占。
/// </remarks>
public sealed class CliChangeListener : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    /// <summary>开始接受连接。重复调用无效果。</summary>
    public void Start()
    {
        _loop ??= Task.Run(() => ListenAsync(_cts.Token));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    CliChangeChannel.PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.Equals(line, CliChangeChannel.ChangedLine, StringComparison.Ordinal))
                {
                    WeakReferenceMessenger.Default.Send(new CliStoreChangedMessage());
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                AppLog.Write("CliChangeListener", ex);
                try
                {
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
