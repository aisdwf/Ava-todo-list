using System.Diagnostics;
using System.IO.Pipes;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 进程互斥：本机所有 FlowTask 副本共用一把锁。后来者请旧进程退出后接管
/// （spec-close-to-tray D5：多 preview exe 关旧开新，而不是唤回旧窗口）。
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>会话内互斥名。preview/dev 与 preview/feature/* 以及 dotnet run 都走这一把。</summary>
    public const string MutexName = @"Local\FlowTask.SingleInstance";

    /// <summary>旧进程监听替换请求的管道名。</summary>
    public const string PipeName = "FlowTask.SingleInstance.Pipe";

    private const string ReplaceCommand = "replace";

    private static readonly TimeSpan GracefulWait = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan AfterKillWait = TimeSpan.FromSeconds(2);
    private static readonly string[] ProcessNames = ["FlowTask", "FlowTask.Desktop"];

    private readonly Mutex _mutex;
    private CancellationTokenSource? _listenCts;
    private bool _ownsMutex;

    private SingleInstanceGuard(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    /// <summary>当前进程持有的守卫；未抢到锁时为 null，启动路径仍继续以免 exe 打不开。</summary>
    public static SingleInstanceGuard? Current { get; private set; }

    /// <summary>
    /// 成为唯一实例。若已有实例，先请它彻底退出；超时则强杀其它 FlowTask 进程。
    /// 仍拿不到锁时返回 null，调用方照常启动。
    /// </summary>
    public static SingleInstanceGuard? AcquireOrReplacePrevious()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            return Current = new SingleInstanceGuard(mutex, ownsMutex: true);
        }

        RequestPreviousInstanceExit();
        if (TryWaitForOwnership(mutex, GracefulWait))
        {
            return Current = new SingleInstanceGuard(mutex, ownsMutex: true);
        }

        KillOtherFlowTaskProcesses();
        if (TryWaitForOwnership(mutex, AfterKillWait))
        {
            return Current = new SingleInstanceGuard(mutex, ownsMutex: true);
        }

        mutex.Dispose();
        return Current = null;
    }

    /// <summary>
    /// 在 UI 启动后监听后来者的替换请求，收到后走与托盘「退出」相同的彻底退出。
    /// </summary>
    public void ListenForReplacement(Action onReplaceRequested)
    {
        _listenCts = new CancellationTokenSource();
        var token = _listenCts.Token;
        _ = Task.Run(() => ListenLoop(onReplaceRequested, token), token);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _listenCts?.Cancel();
        _listenCts?.Dispose();
        _listenCts = null;

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 已被放弃或未持有：退出路径仍须释放底层句柄
            }

            _ownsMutex = false;
        }

        _mutex.Dispose();
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    private static bool TryWaitForOwnership(Mutex mutex, TimeSpan timeout)
    {
        try
        {
            return mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            // 旧进程崩溃而未释放：WaitOne 仍把所有权交给本进程
            return true;
        }
    }

    private static void RequestPreviousInstanceExit()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(800);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(ReplaceCommand);
        }
        catch (Exception)
        {
            // 旧进程正在退出或尚未挂上管道：后续靠 Mutex 等待 / 强杀
        }
    }

    private static void KillOtherFlowTaskProcesses()
    {
        var self = Environment.ProcessId;
        foreach (var name in ProcessNames)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var process in processes)
            {
                try
                {
                    if (process.Id == self)
                    {
                        continue;
                    }

                    process.Kill();
                }
                catch (Exception)
                {
                    // 权限或进程已退出：忽略，外层仍会再等 Mutex
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    private static async Task ListenLoop(Action onReplaceRequested, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (string.Equals(line, ReplaceCommand, StringComparison.OrdinalIgnoreCase))
                {
                    onReplaceRequested();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
