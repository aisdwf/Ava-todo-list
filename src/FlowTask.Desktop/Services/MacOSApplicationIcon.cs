using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Threading;

namespace FlowTask.Desktop.Services;

/// <summary>
/// Sets the running application's Dock icon when launched without a macOS app bundle.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacOSApplicationIcon
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";

    /// <summary>
    /// Applies the SVG-derived image after Avalonia has initialized AppKit.
    /// </summary>
    internal static void Apply()
    {
        Dispatcher.UIThread.VerifyAccess();
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "flowtask-icon.png");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The macOS application icon is missing.", path);
        }

        var nativePath = SendString(
            Send(GetClass("NSString"), Selector("alloc")),
            Selector("initWithUTF8String:"), path);
        try
        {
            var image = SendObject(
                Send(GetClass("NSImage"), Selector("alloc")),
                Selector("initWithContentsOfFile:"), nativePath);
            if (image == IntPtr.Zero)
            {
                throw new InvalidOperationException("AppKit could not decode the FlowTask application icon.");
            }

            try
            {
                var application = Send(GetClass("NSApplication"), Selector("sharedApplication"));
                // Window.Icon does not set the process-wide Dock image on macOS.
                SendVoidObject(application, Selector("setApplicationIconImage:"), image);
            }
            finally
            {
                // NSApplication retains the image; release our alloc/init ownership.
                SendVoid(image, Selector("release"));
            }
        }
        finally
        {
            SendVoid(nativePath, Selector("release"));
        }
    }

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveCLibrary, EntryPoint = "sel_registerName")]
    private static extern IntPtr Selector([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendObject(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendString(
        IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string argument);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendVoidObject(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(IntPtr receiver, IntPtr selector);
}
