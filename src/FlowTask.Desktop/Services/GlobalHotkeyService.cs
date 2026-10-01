using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 进程级热键：Windows 使用 <c>RegisterHotKey</c>，macOS 使用 Carbon Event Hot Key；
/// 其它平台返回 false 并由窗内回退。
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x46_54; // "FT"
    private const uint ModAlt = 0x0001;
    private const uint ModWin = 0x0008;
    private const uint ModNorepeat = 0x4000;
    private const uint VkSpace = 0x20;

    private readonly Action _onHotkey;
    private readonly TaskCompletionSource<bool> _registration =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Thread? _messageThread;
    private volatile bool _running;
    private IntPtr _hwnd;
    private volatile bool _registered;
    private readonly List<IntPtr> _macHotKeys = [];
    private MacEventHandlerProc? _macEventHandler;
    private IntPtr _macEventHandlerRef;
    private GCHandle _macUserData;

    public GlobalHotkeyService(Action onHotkey)
    {
        _onHotkey = onHotkey;
    }

    /// <summary>
    /// 尝试注册全局热键。Windows 使用 Alt+Space；macOS 使用 Option+Space。
    /// 返回是否注册成功。
    /// </summary>
    public bool TryStart()
    {
        if (OperatingSystem.IsWindows())
        {
            return TryStartWindows();
        }

        return OperatingSystem.IsMacOS() && TryStartMacOS();
    }

    private bool TryStartWindows()
    {
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

        if (HotkeyRegistrationWait.Wait(_registration, HotkeyRegistrationWait.DefaultTimeout))
        {
            return true;
        }

        // 超时仍未发布结果：停掉消息线程，避免随后注册成功却与窗内 KeyDown 双路径并存。
        if (!_registration.Task.IsCompleted)
        {
            Dispose();
        }

        return false;
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
                Id = HotkeyId
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

    private void MessageLoop()
    {
        try
        {
            _hwnd = CreateMessageWindow();
            // Alt+Space；部分环境被 shell 占用时再试 Win+Alt+Space
            _registered = RegisterHotKey(_hwnd, HotkeyId, ModAlt | ModNorepeat, VkSpace)
                          || RegisterHotKey(_hwnd, HotkeyId, ModAlt | ModWin | ModNorepeat, VkSpace);
            _registration.TrySetResult(_registered);

            if (!_registered)
            {
                return;
            }

            while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == 0x0312 && (int)msg.wParam == HotkeyId)
                {
                    Dispatcher.UIThread.Post(() => _onHotkey());
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        catch (Exception ex)
        {
            _registration.TrySetResult(false);
            AppLog.Write("GlobalHotkey MessageLoop", ex);
        }
        finally
        {
            if (_hwnd != IntPtr.Zero)
            {
                if (_registered)
                {
                    UnregisterHotKey(_hwnd, HotkeyId);
                    _registered = false;
                }

                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        if (_hwnd != IntPtr.Zero)
        {
            PostMessage(_hwnd, 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
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

    [DllImport("user32.dll")]
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
