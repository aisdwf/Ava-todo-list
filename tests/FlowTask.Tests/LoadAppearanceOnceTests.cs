using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 启动只经 InitializeAsync 加载外观一次（扫描 L4）。
/// </summary>
public class LoadAppearanceOnceTests
{
    [Fact]
    public void MainWindowOpened_DoesNotCallLoadAppearanceBeforeInitialize()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "FlowTask.Desktop", "Views", "MainWindow.axaml.cs"));
        Assert.DoesNotContain("await vm.LoadAppearanceAsync();", text, StringComparison.Ordinal);
        Assert.Contains("await vm.InitializeAsync();", text, StringComparison.Ordinal);
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
