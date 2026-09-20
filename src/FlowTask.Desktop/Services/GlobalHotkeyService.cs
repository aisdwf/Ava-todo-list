using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 进程级热键：Windows 上 <c>RegisterHotKey</c> 近似全局；其它平台返回 false 并由窗内回退。
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x46_54; // "FT"
    private const uint ModAlt = 0x0001;
    private const uint ModWin = 0x0008;
    private const uint ModNorepeat = 0x4000;
    private const uint VkSpace = 0x20;

    private readonly Action _onHotkey;
    private Thread? _messageThread;
    private volatile bool _running;
    private IntPtr _hwnd;
    private bool _registered;

    public GlobalHotkeyService(Action onHotkey)
    {
        _onHotkey = onHotkey;
    }

    /// <summary>
    /// 尝试注册全局热键。Windows：Alt+Space。返回是否注册成功。
    /// </summary>
    public bool TryStart()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        _running = true;
        _messageThread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "FlowTask.GlobalHotkey"
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();

        // 给消息循环一点时间完成 RegisterHotKey
        Thread.Sleep(50);
        return _registered;
    }

    private void MessageLoop()
    {
        try
        {
            _hwnd = CreateMessageWindow();
            // Alt+Space；部分环境被 shell 占用时再试 Win+Alt+Space
            _registered = RegisterHotKey(_hwnd, HotkeyId, ModAlt | ModNorepeat, VkSpace)
                          || RegisterHotKey(_hwnd, HotkeyId, ModAlt | ModWin | ModNorepeat, VkSpace);

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

    private static readonly WndProcDelegate WndProc = (_, msg, wParam, lParam)
        => DefWindowProc(IntPtr.Zero, msg, wParam, lParam);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

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
}
