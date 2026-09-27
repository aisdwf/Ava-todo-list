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
        Assert.Contains("tests 272 passing", text, StringComparison.Ordinal);
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
    public void ConfirmDeleteProjectComments_CascadeDeleteNotRemount()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "FlowTask.Desktop", "ViewModels", "MainViewModel.cs"));
        Assert.Contains("物理删除其下任务", text, StringComparison.Ordinal);
        Assert.DoesNotContain("其下任务不会被删除", text, StringComparison.Ordinal);
        Assert.DoesNotContain("置空退回未归属", text, StringComparison.Ordinal);
        Assert.Contains("不改已保存的当前值", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateClass1Files_AreRemoved()
    {
        var root = FindRepoRoot();
        Assert.False(File.Exists(Path.Combine(root, "src", "FlowTask.Core", "Class1.cs")));
        Assert.False(File.Exists(Path.Combine(root, "src", "FlowTask.Infrastructure", "Class1.cs")));
    }

    [Fact]
    public void TaskFilter_HasNoSettingsMember()
    {
        Assert.DoesNotContain("Settings", Enum.GetNames<FlowTask.Desktop.ViewModels.TaskFilter>());
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
