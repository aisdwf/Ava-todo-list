using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FlowTask.Desktop.Controls;
using FlowTask.Desktop.ViewModels;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 到期日选择器控件的打开 / 提交 / 取消链路（spec-due-date-picker）。
/// </summary>
/// <remarks>
/// 视觉过渡与「不推动布局」只能人工目测；此处只锁定行为契约：
/// 入口可点击打开、提交后关闭并携带目标执行命令、Esc 关闭不提交。
/// </remarks>
public class DueDatePickerTests
{
    private readonly FakeClock _clock = new(
        new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 3, 10));

    private (Window Window, DueDatePicker Picker, List<DueDateCommit> Commits) Mount(object? target = null)
    {
        var commits = new List<DueDateCommit>();
        var picker = new DueDatePicker
        {
            Editor = new DueDateEditorViewModel(_clock, () => 2),
            CommitCommand = new RelayCommand<DueDateCommit>(c => commits.Add(c!)),
            CommitTarget = target
        };
        var window = new Window { Content = picker, Width = 600, Height = 500 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, picker, commits);
    }

    [AvaloniaFact]
    public void ClickingTrigger_OpensPopup()
    {
        var (_, picker, _) = Mount();

        picker.FindControl<Button>("Trigger")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(picker.IsOpen);
    }

    [AvaloniaFact]
    public void Preset_CommitsWithTargetAndCloses()
    {
        var target = new object();
        var (_, picker, commits) = Mount(target);

        picker.Open();
        picker.Editor!.ApplyDefaultDueCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(picker.IsOpen);
        var commit = Assert.Single(commits);
        Assert.Same(target, commit.Target);
        Assert.Equal(new DateTime(2026, 3, 12), commit.Date);
    }

    [AvaloniaFact]
    public void Escape_ClosesWithoutCommitting()
    {
        var (window, picker, commits) = Mount();

        picker.Open();
        Dispatcher.UIThread.RunJobs();
        picker.Editor!.DigitText = "15";
        picker.FindControl<TextBox>("DigitBox")!.Focus();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(picker.IsOpen);
        Assert.Empty(commits);
    }

    [AvaloniaFact]
    public void EnterInDigitBox_CommitsPreview()
    {
        var (window, picker, commits) = Mount();

        picker.Open();
        Dispatcher.UIThread.RunJobs();
        picker.FindControl<TextBox>("DigitBox")!.Focus();
        picker.Editor!.DigitText = "0320";
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(picker.IsOpen);
        Assert.Equal(new DateTime(2026, 3, 20), Assert.Single(commits).Date);
    }

    /// <summary>
    /// 共享编辑器：关闭后的另一实例提交不得回流到本实例的命令。
    /// </summary>
    [AvaloniaFact]
    public void ClosedPicker_IgnoresSharedEditorCommits()
    {
        var (_, picker, commits) = Mount();

        picker.Open();
        picker.Close();
        picker.Editor!.CommitDate(new DateTime(2026, 4, 1));

        Assert.Empty(commits);
    }
}
