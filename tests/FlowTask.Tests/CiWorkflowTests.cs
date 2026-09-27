using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// PR / Release 工作流必须先构建（警告当错误）再测试（扫描 M7）。
/// </summary>
public class CiWorkflowTests
{
    [Fact]
    public void PullRequestWorkflow_BuildsWithWarningsAsErrorsAndRunsTests()
    {
        var ci = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github", "workflows", "ci.yml"));

        Assert.Contains("pull_request", ci, StringComparison.Ordinal);
        Assert.Contains("TreatWarningsAsErrors=true", ci, StringComparison.Ordinal);
        Assert.Contains("dotnet test", ci, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseWorkflow_RunsTestsBeforePublish()
    {
        var release = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github", "workflows", "release-windows.yml"));
        var testIndex = release.IndexOf("dotnet test", StringComparison.Ordinal);
        var publishIndex = release.IndexOf("dotnet publish", StringComparison.Ordinal);

        Assert.True(testIndex >= 0, "release workflow must run tests");
        Assert.True(publishIndex >= 0, "release workflow must still publish");
        Assert.True(testIndex < publishIndex, "tests must run before publish");
        Assert.Contains("TreatWarningsAsErrors=true", release, StringComparison.Ordinal);
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
