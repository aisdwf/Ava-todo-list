using FlowTask.Desktop.Services;
using FlowTask.Desktop.ViewModels;
using FlowTask.Infrastructure.Persistence;
using Avalonia.Headless.XUnit;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 首次聚光灯引导与操作指南（spec-onboarding-guide）。
/// </summary>
/// <remarks>
/// 覆盖的是「引导只在该出现时出现」这一契约：完成或跳过后不再打扰，改版后才再播一次。
/// 挖空对齐与动效观感无法单测，由所有者预览验证。
/// </remarks>
public class OnboardingTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_onboarding_{Guid.NewGuid():N}.db");
    private readonly FakeClock _clock = new(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));

    /// <inheritdoc />
    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // 测试清理阶段的文件锁不影响断言结果
            }
        }
    }

    private MainViewModel CreateViewModel()
        => new(
            new SqliteTaskRepository(_clock, _dbPath),
            new SqliteProjectRepository(_dbPath),
            _clock,
            new SqliteAppSettingsRepository(_dbPath));

    [AvaloniaFact]
    public async Task FreshDatabase_RequestsAutoStart()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.True(vm.Onboarding.ShouldAutoStart);
        Assert.False(vm.Onboarding.IsActive);
    }

    [AvaloniaFact]
    public async Task Finishing_PersistsAndSuppressesNextLaunch()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.StartOnboardingCommand.Execute(null);

        for (var i = 0; i < vm.Onboarding.Steps.Count; i++)
        {
            vm.Onboarding.NextCommand.Execute(null);
        }

        Assert.False(vm.Onboarding.IsActive);
        await vm.Onboarding.CompletionPersistTask;

        var next = CreateViewModel();
        await next.InitializeAsync();
        Assert.False(next.Onboarding.ShouldAutoStart);
    }

    /// <summary>跳过与完成同样记为已看过（design-interaction-principles §2.2 例外条款）。</summary>
    [AvaloniaFact]
    public async Task Skipping_PersistsAndSuppressesNextLaunch()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.StartOnboardingCommand.Execute(null);
        vm.Onboarding.NextCommand.Execute(null);

        vm.Onboarding.SkipCommand.Execute(null);

        Assert.False(vm.Onboarding.IsActive);
        await vm.Onboarding.CompletionPersistTask;
        Assert.Equal(
            OnboardingProgress.ToStorage(OnboardingProgress.CurrentVersion),
            await new SqliteAppSettingsRepository(_dbPath).GetAsync(OnboardingProgress.SettingsKey));

        var next = CreateViewModel();
        await next.InitializeAsync();
        Assert.False(next.Onboarding.ShouldAutoStart);
    }

    [AvaloniaFact]
    public async Task OlderCompletedVersion_AutoStartsAgain()
    {
        var settings = new SqliteAppSettingsRepository(_dbPath);
        await settings.InitializeAsync();
        await settings.SetAsync(
            OnboardingProgress.SettingsKey,
            OnboardingProgress.ToStorage(OnboardingProgress.CurrentVersion - 1));

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        Assert.True(vm.Onboarding.ShouldAutoStart);
    }

    [AvaloniaFact]
    public async Task Replay_FromGuideLeavesSettingsAndStartsAtFirstStep()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.StartOnboardingCommand.Execute(null);
        vm.Onboarding.SkipCommand.Execute(null);
        await vm.Onboarding.CompletionPersistTask;

        vm.OpenGuideCommand.Execute(null);
        Assert.True(vm.IsSettingsOpen);
        Assert.Equal(SettingsSection.Guide, vm.SelectedSettingsSection);

        vm.StartOnboardingCommand.Execute(null);

        // 锚点全在任务页：重播必须先退出设置
        Assert.False(vm.IsSettingsOpen);
        Assert.True(vm.Onboarding.IsActive);
        Assert.Equal(0, vm.Onboarding.StepIndex);
    }

    [AvaloniaFact]
    public void StepNavigation_StaysWithinBounds()
    {
        var vm = CreateViewModel();
        vm.StartOnboardingCommand.Execute(null);
        var onboarding = vm.Onboarding;

        onboarding.PreviousCommand.Execute(null);
        Assert.Equal(0, onboarding.StepIndex);
        Assert.False(onboarding.CanGoBack);
        Assert.True(onboarding.Dots[0].IsCurrent);

        onboarding.NextCommand.Execute(null);
        Assert.Equal(1, onboarding.StepIndex);
        Assert.True(onboarding.CanGoBack);
        Assert.False(onboarding.Dots[0].IsCurrent);
        Assert.True(onboarding.Dots[1].IsCurrent);

        while (!onboarding.IsLastStep)
        {
            onboarding.NextCommand.Execute(null);
        }

        Assert.Equal("开始使用", onboarding.NextText);
        Assert.True(onboarding.IsActive);
    }

    [AvaloniaFact]
    public void ActiveOnboarding_BlocksMainContent()
    {
        var vm = CreateViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.False(vm.IsBlockingOverlayOpen);
        vm.StartOnboardingCommand.Execute(null);
        Assert.True(vm.IsBlockingOverlayOpen);
        Assert.Contains(nameof(MainViewModel.IsBlockingOverlayOpen), raised);

        vm.Onboarding.SkipCommand.Execute(null);
        Assert.False(vm.IsBlockingOverlayOpen);
    }

    [AvaloniaFact]
    public void GuideSection_IsListedBeforeAbout()
    {
        var vm = CreateViewModel();
        var sections = vm.SettingsNavItems.Select(item => item.Section).ToList();

        Assert.True(sections.IndexOf(SettingsSection.Guide) >= 0);
        Assert.Equal(sections.IndexOf(SettingsSection.About) - 1, sections.IndexOf(SettingsSection.Guide));
    }

    [AvaloniaFact]
    public void GuideSelection_NeverBecomesEmpty()
    {
        var guide = new GuideViewModel();
        guide.SelectedTopic = null;

        Assert.Same(guide.Topics[0], guide.SelectedTopic);
    }

    /// <summary>所有者裁决的 6 项全部进入首次引导，且都有锚点与动效场景。</summary>
    [Fact]
    public void Catalog_OnboardingCoversOwnerSelectedTopics()
    {
        var scenes = GuideCatalog.OnboardingSteps.Select(step => step.Scene).ToArray();

        Assert.Equal(
            new[]
            {
                GuideSceneKind.AddTask,
                GuideSceneKind.DueAndPriority,
                GuideSceneKind.EditAndComplete,
                GuideSceneKind.Projects,
                GuideSceneKind.QuickCapture,
                GuideSceneKind.Theme
            },
            scenes);
        Assert.All(GuideCatalog.OnboardingSteps, step => Assert.NotEmpty(step.TargetKeys));
    }

    /// <summary>R-6.2：每个指南条目都有独立的动效场景，没有纯文字条目。</summary>
    [Fact]
    public void Catalog_EveryTopicHasDistinctSceneAndSummary()
    {
        var topics = GuideCatalog.Topics;

        Assert.Equal(Enum.GetValues<GuideSceneKind>().Length, topics.Count);
        Assert.Equal(topics.Count, topics.Select(topic => topic.Scene).Distinct().Count());
        Assert.All(topics, topic =>
        {
            Assert.False(string.IsNullOrWhiteSpace(topic.Title));
            Assert.False(string.IsNullOrWhiteSpace(topic.Summary));
        });
        Assert.Equal("01", topics[0].Number);
        Assert.Equal(topics.Count.ToString("00"), topics[^1].Number);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("  ", 0)]
    [InlineData("abc", 0)]
    [InlineData("-3", 0)]
    [InlineData("1", 1)]
    [InlineData(" 2 ", 2)]
    public void Progress_ParseToleratesBadValues(string? raw, int expected)
        => Assert.Equal(expected, OnboardingProgress.Parse(raw));

    [Theory]
    [InlineData(RegisteredHotkey.AltSpace, false, "Alt+Space")]
    [InlineData(RegisteredHotkey.WinAltSpace, false, "Win+Alt+Space")]
    [InlineData(RegisteredHotkey.None, false, "Alt+Space")]
    [InlineData(RegisteredHotkey.None, true, "⌥ Space")]
    public void HotkeyLabel_FollowsActualRegistration(RegisteredHotkey registered, bool isMacOS, string expected)
        => Assert.Equal(expected, QuickCaptureHotkey.Describe(registered, isMacOS));

    [AvaloniaFact]
    public void HotkeyLabel_UpdatesWhenFallbackRegistered()
    {
        var vm = CreateViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SetRegisteredHotkey(RegisteredHotkey.WinAltSpace);

        Assert.Equal("Win+Alt+Space", vm.QuickCaptureHotkeyLabel);
        Assert.Contains(nameof(MainViewModel.QuickCaptureHotkeyLabel), raised);
    }
}
