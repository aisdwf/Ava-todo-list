using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 进程级快捷小窗快捷键：Windows 使用 <c>RegisterHotKey</c>，组合由用户配置；
/// macOS 使用 Carbon Event Hot Key，暂固定 Option+Space；其它平台不注册，由窗内回退。
/// </summary>
/// <remarks>
/// 此前 Windows 先试 Alt+Space、失败就静默改注册 Win+Alt+Space，用户只看到键帽变了却不知道原因
/// （design wrong，spec-quick-window-custom-hotkey）。现在只注册用户选定的组合，失败如实返回，
/// 由调用方提示并停用；每次注册的结果写入 <see cref="AppLog"/>。
/// </remarks>
public sealed class GlobalHotkeyService : IQuickWindowHotkeyRegistrar, IDisposable
{
    /// <summary>两个 id 轮换：换键时先注册新 id，成功后才注销旧 id，失败则旧组合不受影响。</summary>
    private const int HotkeyIdA = 0x46_54; // "FT"
    private const int HotkeyIdB = 0x46_55;
    private const uint ModNorepeat = 0x4000;
    private const uint WmHotkey = 0x0312;
    private const uint WmQuit = 0x0012;
    private const uint WmAppInvoke = 0x8000 + 0x46; // WM_APP + n：让消息线程执行排队的注册请求
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private readonly Action _onHotkey;
    private readonly object _startGate = new();
    private readonly ConcurrentQueue<Action> _pending = new();
    private TaskCompletionSource<bool>? _ready;
    private Thread? _messageThread;
    private volatile bool _running;
    private IntPtr _hwnd;

    /// <summary>当前生效的 id；0 表示没有注册任何组合。只在消息线程读写。</summary>
    private int _activeId;
    private QuickWindowHotkey _activeHotkey;

    private readonly List<IntPtr> _macHotKeys = [];
    private MacEventHandlerProc? _macEventHandler;
    private IntPtr _macEventHandlerRef;
    private GCHandle _macUserData;

    public GlobalHotkeyService(Action onHotkey)
    {
        _onHotkey = onHotkey;
    }

    /// <inheritdoc />
    public bool SupportsCustomHotkey => OperatingSystem.IsWindows();

    /// <inheritdoc />
    /// <remarks>
    /// TODO(macos-custom-hotkey): [2026-10-15] macOS 需要把 <see cref="QuickWindowHotkey"/> 映射为
    /// Carbon 键码（kVK_*）与修饰位（cmdKey / optionKey / controlKey / shiftKey），
    /// 并按「先注册新、再注销旧」改写 <see cref="TryStartMacOS"/>；完成后让
    /// <see cref="SupportsCustomHotkey"/> 在 macOS 返回 true。期限与 spec-macos-initial-support 一致，
    /// 该 SPEC 的 Risks 中登记了本项。
    /// </remarks>
    public async Task<HotkeyRegistrationOutcome> TryApplyAsync(QuickWindowHotkey hotkey)
    {
        if (!OperatingSystem.IsWindows())
        {
            return HotkeyRegistrationOutcome.Unsupported;
        }

        if (!await EnsureWindowsThreadAsync().ConfigureAwait(false))
        {
            AppLog.Write($"GlobalHotkey Windows message thread unavailable; {hotkey} not registered");
            return HotkeyRegistrationOutcome.Failed;
        }

        var result = new TaskCompletionSource<HotkeyRegistrationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.Enqueue(() => ApplyOnMessageThread(hotkey, result));
        if (!PostMessage(_hwnd, WmAppInvoke, IntPtr.Zero, IntPtr.Zero))
        {
            AppLog.Write($"GlobalHotkey Windows PostMessage failed: {Marshal.GetLastPInvokeError()}");
            return HotkeyRegistrationOutcome.Failed;
        }

        var outcome = await HotkeyRegistrationWait
            .WaitAsync(result, HotkeyRegistrationWait.DefaultTimeout, HotkeyRegistrationOutcome.Failed)
            .ConfigureAwait(false);
        return outcome;
    }

    /// <inheritdoc />
    public Task<bool> TryStartFixedAsync()
        => Task.FromResult(OperatingSystem.IsMacOS() && TryStartMacOS());

    private Task<bool> EnsureWindowsThreadAsync()
    {
        lock (_startGate)
        {
            if (_ready is null)
            {
                _ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _running = true;
                _messageThread = new Thread(MessageLoop)
                {
                    IsBackground = true,
                    Name = "FlowTask.GlobalHotkey"
                };
                if (OperatingSystem.IsWindows())
                {
                    _messageThread.SetApartmentState(ApartmentState.STA);
                }

                _messageThread.Start();
            }
        }

        return HotkeyRegistrationWait.WaitAsync(_ready, HotkeyRegistrationWait.DefaultTimeout, false);
    }

    /// <summary>
    /// 在消息线程上注册：<c>RegisterHotKey</c> 把热键绑在调用线程的窗口上，只能在这里调用。
    /// </summary>
    private void ApplyOnMessageThread(QuickWindowHotkey hotkey, TaskCompletionSource<HotkeyRegistrationOutcome> result)
    {
        // 同一组合再注册会因「已被注册」失败（占用者就是自己），不能误报成被其他程序占用
        if (_activeId != 0 && hotkey == _activeHotkey)
        {
            result.TrySetResult(HotkeyRegistrationOutcome.Registered);
            return;
        }

        var nextId = _activeId == HotkeyIdA ? HotkeyIdB : HotkeyIdA;
        if (!RegisterHotKey(_hwnd, nextId, hotkey.Win32Modifiers | ModNorepeat, hotkey.Win32VirtualKey))
        {
            var error = Marshal.GetLastPInvokeError();
            var outcome = error == ErrorHotkeyAlreadyRegistered
                ? HotkeyRegistrationOutcome.Occupied
                : HotkeyRegistrationOutcome.Failed;
            AppLog.Write($"GlobalHotkey Windows registration failed: {hotkey}, Win32 error {error} ({outcome})");
            result.TrySetResult(outcome);
            return;
        }

        // 调用方已超时放弃：立刻撤掉，避免界面以为失败、系统里却多挂一个热键
        if (!result.TrySetResult(HotkeyRegistrationOutcome.Registered))
        {
            UnregisterHotKey(_hwnd, nextId);
            AppLog.Write($"GlobalHotkey Windows registration of {hotkey} arrived after timeout; unregistered");
            return;
        }

        if (_activeId != 0)
        {
            UnregisterHotKey(_hwnd, _activeId);
            AppLog.Write($"GlobalHotkey Windows unregistered: {_activeHotkey}");
        }

        _activeId = nextId;
        _activeHotkey = hotkey;
        AppLog.Write($"GlobalHotkey Windows registered: {hotkey}");
    }

    private void MessageLoop()
    {
        try
        {
            _hwnd = CreateMessageWindow();
            if (_hwnd == IntPtr.Zero)
            {
                AppLog.Write($"GlobalHotkey Windows CreateWindowEx failed: {Marshal.GetLastPInvokeError()}");
                _ready!.TrySetResult(false);
                return;
            }

            _ready!.TrySetResult(true);

            while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WmAppInvoke)
                {
                    while (_pending.TryDequeue(out var action))
                    {
                        action();
                    }

                    continue;
                }

                if (msg.message == WmHotkey && _activeId != 0 && (int)msg.wParam == _activeId)
                {
                    Dispatcher.UIThread.Post(_onHotkey);
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        catch (Exception ex)
        {
            _ready?.TrySetResult(false);
            AppLog.Write("GlobalHotkey MessageLoop", ex);
        }
        finally
        {
            if (_hwnd != IntPtr.Zero)
            {
                if (_activeId != 0)
                {
                    UnregisterHotKey(_hwnd, _activeId);
                    _activeId = 0;
                }

                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
    }

    private bool TryStartMacOS()
    {
        try
        {
            _macEventHandler = OnMacHotKey;
            _macUserData = GCHandle.Alloc(this);

            var eventType = new EventTypeSpec
            {
                EventClass = EventClassKeyboard,
                EventKind = EventHotKeyPressed
            };

            var status = InstallEventHandler(
                GetApplicationEventTarget(),
                _macEventHandler,
                1,
                ref eventType,
                GCHandle.ToIntPtr(_macUserData),
                out _macEventHandlerRef);
            if (status != 0)
            {
                AppLog.Write($"GlobalHotkey macOS InstallEventHandler failed: {status}");
                DisposeMacOS();
                return false;
            }

            var hotKeyId = new EventHotKeyID
            {
                Signature = MacHotkeySignature,
                Id = HotkeyIdA
            };

            status = RegisterEventHotKey(
                MacSpaceKeyCode,
                MacOptionKey,
                hotKeyId,
                GetApplicationEventTarget(),
                0,
                out var optionHotKey);
            if (status == 0)
            {
                _macHotKeys.Add(optionHotKey);
            }
            else
            {
                AppLog.Write($"GlobalHotkey macOS Option+Space registration failed: {status}");
            }

            // Some macOS configurations reserve Option+Space. Keep a discoverable
            // Command+Option+Space fallback without changing the primary label.
            if (_macHotKeys.Count == 0)
            {
                status = RegisterEventHotKey(
                    MacSpaceKeyCode,
                    MacCommandOptionKey,
                    hotKeyId,
                    GetApplicationEventTarget(),
                    0,
                    out var commandOptionHotKey);
                if (status == 0)
                {
                    _macHotKeys.Add(commandOptionHotKey);
                }
                else
                {
                    AppLog.Write($"GlobalHotkey macOS Command+Option+Space registration failed: {status}");
                }
            }

            if (_macHotKeys.Count == 0)
            {
                DisposeMacOS();
                return false;
            }

            AppLog.Write(
                _macHotKeys.Count == 1
                    ? "GlobalHotkey macOS registered: Option+Space"
                    : "GlobalHotkey macOS registered: Option+Space and fallback");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Write("GlobalHotkey macOS registration", ex);
            DisposeMacOS();
            return false;
        }
    }

    public void Dispose()
    {
        _running = false;
        if (_hwnd != IntPtr.Zero)
        {
            PostMessage(_hwnd, WmQuit, IntPtr.Zero, IntPtr.Zero);
        }

        _messageThread?.Join(500);
        DisposeMacOS();
    }

    private IntPtr OnMacHotKey(IntPtr nextHandler, IntPtr eventRef, IntPtr userData)
    {
        Dispatcher.UIThread.Post(_onHotkey);
        return IntPtr.Zero;
    }

    private void DisposeMacOS()
    {
        foreach (var hotKey in _macHotKeys)
        {
            try
            {
                UnregisterEventHotKey(hotKey);
            }
            catch (Exception ex)
            {
                AppLog.Write("GlobalHotkey macOS unregister", ex);
            }
        }

        _macHotKeys.Clear();

        if (_macEventHandlerRef != IntPtr.Zero)
        {
            try
            {
                RemoveEventHandler(_macEventHandlerRef);
            }
            catch (Exception ex)
            {
                AppLog.Write("GlobalHotkey macOS remove handler", ex);
            }

            _macEventHandlerRef = IntPtr.Zero;
        }

        if (_macUserData.IsAllocated)
        {
            _macUserData.Free();
        }

        _macEventHandler = null;
    }

    private static IntPtr CreateMessageWindow()
    {
        var wndClass = new WndClass
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProc),
            lpszClassName = "FlowTaskHotkeyHiddenWindow"
        };
        RegisterClass(ref wndClass);
        return CreateWindowEx(
            0,
            wndClass.lpszClassName,
            string.Empty,
            0,
            0, 0, 0, 0,
            new IntPtr(-3), // HWND_MESSAGE
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
    }

    private static readonly WndProcDelegate WndProc = (hWnd, msg, wParam, lParam)
        => DefWindowProc(hWnd, msg, wParam, lParam);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr MacEventHandlerProc(
        IntPtr nextHandler,
        IntPtr eventRef,
        IntPtr userData);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint EventClass;
        public uint EventKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyID
    {
        public uint Signature;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WndClass
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    private const uint EventClassKeyboard = 0x6B657962; // 'keyb'
    private const uint EventHotKeyPressed = 5;
    private const uint MacHotkeySignature = 0x46544B59; // 'FTKY'
    private const uint MacSpaceKeyCode = 49;
    private const uint MacOptionKey = 1u << 11;
    private const uint MacCommandOptionKey = (1u << 8) | (1u << 11);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClass(ref WndClass lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern IntPtr GetApplicationEventTarget();

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern int InstallEventHandler(
        IntPtr target,
        MacEventHandlerProc handler,
        uint eventTypeCount,
        ref EventTypeSpec eventTypes,
        IntPtr userData,
        out IntPtr handlerRef);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern int RemoveEventHandler(IntPtr handlerRef);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern int RegisterEventHotKey(
        uint keyCode,
        uint modifiers,
        EventHotKeyID hotKeyId,
        IntPtr eventTarget,
        uint options,
        out IntPtr hotKeyRef);

    [DllImport("/System/Library/Frameworks/Carbon.framework/Carbon")]
    private static extern int UnregisterEventHotKey(IntPtr hotKeyRef);
}
