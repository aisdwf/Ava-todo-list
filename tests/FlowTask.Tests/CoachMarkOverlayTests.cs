using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlowTask.Desktop.ViewModels;
using FlowTask.Desktop.Views;
using FlowTask.Desktop.Views.Onboarding;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 在真实主窗里走一遍聚光灯引导：挖空框住锚点、键盘推进、Esc 跳过（spec-onboarding-guide）。
/// </summary>
/// <remarks>
/// ViewModel 单测覆盖不到 XAML 绑定与定位代码；这里是它们唯一的自动化检查。
/// 观感（动效节奏、压暗程度）仍需所有者预览。
/// </remarks>
public class CoachMarkOverlayTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_coach_{Guid.NewGuid():N}.db");
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

    private async Task<(MainWindow Window, MainViewModel Vm)> OpenAsync(double width = 1180, double height = 780)
    {
        var tasks = new SqliteTaskRepository(_clock, _dbPath);
        var projects = new SqliteProjectRepository(_dbPath);
        var settings = new SqliteAppSettingsRepository(_dbPath);
        var vm = new MainViewModel(tasks, projects, _clock, settings);
        var quick = new QuickCaptureViewModel(tasks, projects, settings, _clock);

        await vm.InitializeAsync();
        var window = new MainWindow(vm, quick) { Width = width, Height = height };
        window.Show();
        Flush(window);
        return (window, vm);
    }

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static Rect BoundsIn(Control control, Visual relativeTo)
        => new(control.TranslatePoint(default, relativeTo)!.Value, control.Bounds.Size);

    /// <summary>默认尺寸与最小尺寸（MinWidth 940 × MinHeight 600）都要放得下气泡。</summary>
    [AvaloniaTheory]
    [InlineData(1180, 780, false)]
    [InlineData(940, 600, false)]
    [InlineData(1180, 780, true)]
    public async Task Spotlight_WrapsEachStepAnchor(double width, double height, bool withTask)
    {
        if (withTask)
        {
            // 有任务时「编辑与完成」应指向任务行而非空状态
            var seed = new SqliteTaskRepository(_clock, _dbPath);
            var vmSeed = new MainViewModel(seed, new SqliteProjectRepository(_dbPath), _clock, new SqliteAppSettingsRepository(_dbPath));
            await vmSeed.InitializeAsync();
            vmSeed.NewTaskTitle = "示例任务";
            await vmSeed.AddTaskCommand.ExecuteAsync(null);
        }

        var (window, vm) = await OpenAsync(width, height);
        var overlay = window.GetVisualDescendants().OfType<CoachMarkOverlay>().Single();
        Assert.False(overlay.IsVisible);

        vm.StartOnboardingCommand.Execute(null);
        Flush(window);
        Assert.True(overlay.IsVisible);

        var stage = window.GetVisualDescendants().OfType<Canvas>().Single(canvas => canvas.Name == "Stage");
        for (var step = 0; step < vm.Onboarding.Steps.Count; step++)
        {
            Flush(window);
            var keys = vm.Onboarding.CurrentStep.TargetKeys;
            var anchor = window.GetVisualDescendants()
                .OfType<Control>()
                .First(control => keys.Contains(OnboardingAnchor.GetKey(control))
                                  && control.IsEffectivelyVisible
                                  && control.Bounds.Width > 0);

            var spotRect = overlay.SpotTarget!.Value;
            var anchorRect = BoundsIn(anchor, stage);

            // 挖空包住锚点（允许贴边收回的 2px）
            Assert.True(
                spotRect.Inflate(2).Contains(anchorRect.Intersect(new Rect(stage.Bounds.Size).Deflate(2))),
                $"step {step} ({vm.Onboarding.CurrentStep.Title}): spot {spotRect} does not wrap anchor {anchorRect}");

            // 气泡完整落在窗口内，且不压住挖空
            var bubble = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Bubble");
            var bubbleRect = new Rect(overlay.BubbleTarget!.Value, bubble.DesiredSize);
            Assert.True(new Rect(stage.Bounds.Size).Contains(bubbleRect), $"step {step}: bubble {bubbleRect} leaves the window");
            Assert.False(bubbleRect.Intersects(spotRect), $"step {step}: bubble {bubbleRect} covers spot {spotRect}");

            vm.Onboarding.NextCommand.Execute(null);
        }

        Assert.False(vm.Onboarding.IsActive);
        window.Close();
    }

    /// <summary>空列表时「编辑与完成」退回指向空状态区，不指向不存在的任务行（Q2）。</summary>
    [AvaloniaFact]
    public async Task EmptyList_EditStepFallsBackToEmptyState()
    {
        var (window, vm) = await OpenAsync();
        Assert.True(vm.IsTaskStreamEmpty);

        vm.StartOnboardingCommand.Execute(null);
        vm.Onboarding.NextCommand.Execute(null);
        vm.Onboarding.NextCommand.Execute(null);
        Flush(window);
        Assert.Equal(GuideSceneKind.EditAndComplete, vm.Onboarding.CurrentStep.Scene);

        var empty = window.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => OnboardingAnchor.GetKey(control) == OnboardingTargets.TaskEmpty);
        var stage = window.GetVisualDescendants().OfType<Canvas>().Single(canvas => canvas.Name == "Stage");
        var overlay = window.GetVisualDescendants().OfType<CoachMarkOverlay>().Single();

        Assert.True(overlay.SpotTarget!.Value.Inflate(2).Contains(BoundsIn(empty, stage)));
        window.Close();
    }

    /// <summary>
    /// 遮罩期间焦点常停在新任务输入框：回车必须推进引导，而不是建出一条任务。
    /// </summary>
    [AvaloniaFact]
    public async Task Keyboard_AdvancesWithoutTypingIntoInputs()
    {
        var (window, vm) = await OpenAsync();
        var input = window.FindControl<TextBox>("NewTaskInput")!;
        input.Focus();

        vm.StartOnboardingCommand.Execute(null);
        Flush(window);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Flush(window);
        Assert.Equal(1, vm.Onboarding.StepIndex);

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Flush(window);
        Assert.Equal(0, vm.Onboarding.StepIndex);

        window.KeyTextInput("a");
        Flush(window);
        Assert.Empty(vm.NewTaskTitle);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Flush(window);
        Assert.False(vm.Onboarding.IsActive);
        Assert.Empty(vm.Tasks);
        await vm.Onboarding.CompletionPersistTask;
        window.Close();
    }

    [AvaloniaFact]
    public async Task HelpButton_OpensGuideSection()
    {
        var (window, vm) = await OpenAsync();

        var help = window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => ToolTip.GetTip(button) as string == "操作指南");
        help.Command!.Execute(null);
        Flush(window);

        Assert.True(vm.IsSettingsOpen);
        Assert.Equal(SettingsSection.Guide, vm.SelectedSettingsSection);
        Assert.False(help.IsEffectivelyVisible);
        window.Close();
    }
}
