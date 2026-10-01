using System.Runtime.InteropServices;

namespace FlowTask.Desktop.Services;

/// <summary>
/// 启动失败时的本机提示。Avalonia 尚未起来，只能走系统对话框。
/// </summary>
internal static class NativeUserAlert
{
    public static void Show(string message)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _ = MessageBoxW(IntPtr.Zero, message, "FlowTask", 0x00000010);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
