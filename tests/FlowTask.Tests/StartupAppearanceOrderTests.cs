using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 锁住启动顺序：已保存风格必须在主窗构造 / Show 之前写进主题字典。
/// </summary>
public class StartupAppearanceOrderTests
{
    [Fact]
    public void App_LoadsAppearanceBeforeConstructingMainWindow()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "FlowTask.Desktop", "App.axaml.cs"));
        var load = text.IndexOf("await mainVm.LoadAppearanceAsync()", StringComparison.Ordinal);
        var construct = text.IndexOf("new MainWindow(", StringComparison.Ordinal);

        Assert.True(load >= 0, "App must call LoadAppearanceAsync before showing the main window.");
        Assert.True(construct >= 0, "App must construct MainWindow after loading appearance.");
        Assert.True(load < construct, "LoadAppearanceAsync must run before new MainWindow.");
    }

    [Fact]
    public void MainWindowConstructor_AppliesMaterialBeforeOpened()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "FlowTask.Desktop", "Views", "MainWindow.axaml.cs"));
        var ctor = text.IndexOf("public MainWindow(MainViewModel vm", StringComparison.Ordinal);
        var apply = text.IndexOf("AppearanceCoordinator.ApplyMaterial(this", ctor, StringComparison.Ordinal);
        var opened = text.IndexOf("Opened +=", ctor, StringComparison.Ordinal);

        Assert.True(ctor >= 0 && apply > ctor && opened > apply);
    }

    [Fact]
    public void MainWindowOpened_DoesNotApplyAppearanceAfterInitialize()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "FlowTask.Desktop", "Views", "MainWindow.axaml.cs"));
        var opened = text.IndexOf("private async Task OnOpenedAsync()", StringComparison.Ordinal);
        var nextMember = text.IndexOf("public void SetSystemHotkeyActive", opened, StringComparison.Ordinal);
        Assert.True(opened >= 0 && nextMember > opened);

        var body = text[opened..nextMember];
        Assert.DoesNotContain("AppearanceCoordinator.ApplyTheme", body, StringComparison.Ordinal);
        Assert.DoesNotContain("AppearanceCoordinator.ApplyMaterial", body, StringComparison.Ordinal);
        Assert.Contains("await vm.InitializeAsync();", body, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FlowTask.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("FlowTask.sln not found above the test output directory.");
    }
}
