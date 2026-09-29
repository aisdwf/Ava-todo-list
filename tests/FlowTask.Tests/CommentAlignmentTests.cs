using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 注释与基线不得再描述已删除的「未归属 / TaskTags / 163 条测试」（扫描 M6）。
/// </summary>
public class CommentAlignmentTests
{
    /// <summary>扫描 M6 时的测试数。基线只能随新增测试上调，不得回落到它以下。</summary>
    private const int ScanTestCount = 272;

    /// <remarks>
    /// 此前断言字面量 <c>tests 272 passing</c>：名为「至少」，实为「恰好」，
    /// 任何正当的基线上调都会让它失败（spec-onboarding-guide 合入 dev 时暴露）。
    /// </remarks>
    [Fact]
    public void AgentsBaseline_IsAtLeastScanCount()
    {
        var text = File.ReadAllText(Path.Combine(FindRepoRoot(), "AGENTS.md"));
        var match = System.Text.RegularExpressions.Regex.Match(text, @"tests (\d+) passing");

        Assert.True(match.Success, "AGENTS.md must state the test baseline as 'tests N passing'.");
        Assert.True(
            int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= ScanTestCount,
            $"AGENTS.md test baseline must not fall below the scan count {ScanTestCount}.");
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
