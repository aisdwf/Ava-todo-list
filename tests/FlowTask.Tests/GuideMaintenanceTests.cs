using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using FlowTask.Desktop.ViewModels;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 操作指南随功能同步维护的机器门禁（project-rules BR-1）。
/// </summary>
/// <remarks>
/// 指南教的是具体手势、按键与控件位置，功能一改就会悄悄过期，而编译与其它测试都不会报错。
/// 本测试只读文件内容、不读 git 历史，任务分支、dev、main 上行为一致。
/// </remarks>
public class GuideMaintenanceTests
{
    /// <summary>BR-1 生效日。此前创建的 SPEC 不追溯。</summary>
    private static readonly DateTime AdoptionDate = new(2026, 9, 29);

    [Fact]
    public void EveryTourAnchor_IsReferencedByAView()
    {
        var root = RepoRoot();
        var views = Directory.GetFiles(Path.Combine(root, "src", "FlowTask.Desktop", "Views"), "*.axaml", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        var constants = typeof(OnboardingTargets)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => field.Name)
            .ToArray();

        Assert.NotEmpty(constants);
        foreach (var name in constants)
        {
            Assert.True(
                views.Any(text => text.Contains($"OnboardingTargets.{name}", StringComparison.Ordinal)),
                $"OnboardingTargets.{name} is not attached to any control; the tour step pointing at it would fall back to the window center.");
        }
    }

    [Fact]
    public void EveryTourTarget_IsAKnownAnchor()
    {
        var known = typeof(OnboardingTargets)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet();

        Assert.All(
            GuideCatalog.Topics.SelectMany(topic => topic.TargetKeys),
            key => Assert.Contains(key, known));
    }

    /// <summary>
    /// 生效日之后创建、带实施清单的 SPEC 必须写明指南是否受影响（BR-1 第 4 条）。
    /// </summary>
    [Fact]
    public void NewSpecsWithChecklist_DeclareGuideImpact()
    {
        var specsRoot = Path.Combine(RepoRoot(), "docs", "specs");
        var created = new Regex(@"\*\*Created Date\*\*:\s*(\d{4}-\d{2}-\d{2})", RegexOptions.CultureInvariant);
        var guideLine = new Regex(@"^\s*- \[[ x]\] Guide:", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var offenders = new List<string>();

        foreach (var path in Directory.GetFiles(specsRoot, "spec-*.md", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            var match = created.Match(text);
            if (!match.Success
                || DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture) < AdoptionDate
                || !ChecklistOf(text, out var checklist))
            {
                continue;
            }

            if (!guideLine.IsMatch(checklist))
            {
                offenders.Add(Path.GetRelativePath(specsRoot, path));
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These SPECs must add a '- [x] Guide: updated …' or '- [x] Guide: not affected — …' line to their Change checklist " +
            "(docs/rules/project-rules.md BR-1): " + string.Join(", ", offenders));
    }

    private static bool ChecklistOf(string spec, out string checklist)
    {
        const string heading = "## Change checklist";
        var start = spec.IndexOf(heading, StringComparison.Ordinal);
        if (start < 0)
        {
            checklist = string.Empty;
            return false;
        }

        start += heading.Length;
        var end = spec.IndexOf("\n## ", start, StringComparison.Ordinal);
        checklist = end < 0 ? spec[start..] : spec[start..end];
        return true;
    }

    private static string RepoRoot()
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
