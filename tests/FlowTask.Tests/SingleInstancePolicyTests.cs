using FlowTask.Desktop.Services;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 单实例拿不到锁必须退出；强杀与管道替换按可执行路径授权（扫描 M8）。
/// </summary>
public class SingleInstancePolicyTests
{
    [Fact]
    public void ReplacementSupport_MatchesCurrentOperatingSystem()
    {
        Assert.Equal(OperatingSystem.IsWindows(), SingleInstanceGuard.SupportsReplacement);
    }

    [Fact]
    public void MustExit_WhenMutexNotOwned()
    {
        Assert.True(SingleInstancePolicy.MustExit(ownsMutex: false));
        Assert.False(SingleInstancePolicy.MustExit(ownsMutex: true));
    }

    [Fact]
    public void CanKillProcess_RequiresSameFullPath()
    {
        var self = Path.Combine(Path.GetTempPath(), "preview", "dev", "FlowTask.exe");
        var otherPreview = Path.Combine(Path.GetTempPath(), "preview", "feature", "x", "FlowTask.exe");

        Assert.True(SingleInstancePolicy.CanKillProcess(self, self));
        Assert.False(SingleInstancePolicy.CanKillProcess(self, otherPreview));
        Assert.False(SingleInstancePolicy.CanKillProcess(self, null));
        Assert.False(SingleInstancePolicy.CanKillProcess(null, self));
    }

    [Fact]
    public void IsAuthorizedReplace_RejectsBareReplaceCommand()
    {
        var exe = Path.Combine(Path.GetTempPath(), "FlowTask.exe");

        Assert.False(SingleInstancePolicy.IsAuthorizedReplace("replace", exe, exe));
        Assert.False(SingleInstancePolicy.IsAuthorizedReplace(
            SingleInstancePolicy.FormatReplaceLine(exe),
            exe,
            clientExecutablePath: Path.Combine(Path.GetTempPath(), "other", "FlowTask.exe")));
    }

    [Fact]
    public void IsAuthorizedReplace_AcceptsSamePathClientAndClaim()
    {
        var exe = Path.Combine(Path.GetTempPath(), "FlowTask.exe");

        Assert.True(SingleInstancePolicy.IsAuthorizedReplace(
            SingleInstancePolicy.FormatReplaceLine(exe),
            exe,
            exe));
    }
}

/// <summary>
/// 启动路径必须在拿不到锁时退出。
/// </summary>
public class SingleInstanceStartupTests
{
    [Fact]
    public void ProgramMain_ExitsWhenGuardReturnsNull()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "FlowTask.Desktop", "Program.cs"));
        Assert.Contains("AcquireOrReplacePrevious", program, StringComparison.Ordinal);
        Assert.Contains("instance is null", program, StringComparison.Ordinal);
        Assert.Contains("ExitCode = 1", program, StringComparison.Ordinal);
        Assert.Contains("NativeUserAlert.Show", program, StringComparison.Ordinal);
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
