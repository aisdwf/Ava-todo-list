using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 注释与基线不得再描述已删除的「未归属 / TaskTags / 163 条测试」（扫描 M6）。
/// </summary>
public class CommentAlignmentTests
{
    [Fact]
    public void AgentsBaseline_IsAtLeastScanCount()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(), "AGENTS.md"));
        Assert.Contains("tests 218 passing", text, StringComparison.Ordinal);
        Assert.DoesNotContain("tests 163 passing", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskItemComments_DoNotMentionRemovedTaskTags()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "FlowTask.Core", "Models", "TaskItem.cs"));
        Assert.DoesNotContain("TaskTags", text, StringComparison.Ordinal);
        Assert.Contains("Default", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfirmDeleteProjectComments_ReassignDefaultNotNull()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "FlowTask.Desktop", "ViewModels", "MainViewModel.cs"));
        Assert.Contains("改挂 Default", text, StringComparison.Ordinal);
        Assert.DoesNotContain("置空退回未归属", text, StringComparison.Ordinal);
        Assert.Contains("不改内存里的当前值", text, StringComparison.Ordinal);
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
