using Avalonia;
using System;
using FlowTask.Desktop.Services;

namespace FlowTask.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppLog.Write("Starting application");
        using var instance = SingleInstanceGuard.AcquireOrReplacePrevious();
        if (instance is null)
        {
            AppLog.Write(SingleInstancePolicy.FailureMessage);
            NativeUserAlert.Show(SingleInstancePolicy.FailureMessage);
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLog.Write("Fatal startup", ex);
            Environment.ExitCode = 1;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

}
