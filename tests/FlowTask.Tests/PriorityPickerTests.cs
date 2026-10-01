using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FlowTask.Core.Enums;
using FlowTask.Desktop.Controls;
using FlowTask.Desktop.ViewModels;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 优先级选择器的打开 / 点选提交 / 取消链路（spec-inline-task-edit）。
/// </summary>
/// <remarks>
/// 与 <see cref="DueDatePickerTests"/> 同理：视觉只能人工目测，此处锁定行为契约。
/// </remarks>
public class PriorityPickerTests
{
    private static (Window Window, PriorityPicker Picker, List<PriorityCommit> Commits) Mount(
        TaskPriority priority = TaskPriority.Medium,
        object? target = null)
    {
        var commits = new List<PriorityCommit>();
        var picker = new PriorityPicker
        {
            Priority = priority,
            CommitCommand = new RelayCommand<PriorityCommit>(c => commits.Add(c!)),
            CommitTarget = target
        };
        var window = new Window { Content = picker, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, picker, commits);
    }

    [AvaloniaFact]
    public void ClickingTag_OpensPopup()
    {
        var (_, picker, _) = Mount();

        picker.FindControl<Button>("Trigger")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(picker.IsOpen);
    }

    [AvaloniaFact]
    public void PickingOption_CommitsWithTargetAndCloses()
    {
        var target = new object();
        var (_, picker, commits) = Mount(TaskPriority.Low, target);

        picker.Open();
        Dispatcher.UIThread.RunJobs();
        picker.FindControl<Button>("OptionHigh")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.False(picker.IsOpen);
        var commit = Assert.Single(commits);
        Assert.Same(target, commit.Target);
        Assert.Equal(TaskPriority.High, commit.Priority);
    }

    [AvaloniaFact]
    public void Escape_ClosesWithoutCommitting()
    {
        var (window, picker, commits) = Mount();

        picker.Open();
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(picker.IsOpen);
        Assert.Empty(commits);
    }

    /// <summary>打开时焦点落在当前档，方向键 + Enter 可完成选择（键盘路径）。</summary>
    [AvaloniaFact]
    public void Open_FocusesCurrentOption_ArrowAndEnterCommit()
    {
        var (window, picker, commits) = Mount(TaskPriority.Medium);

        picker.Open();
        Dispatcher.UIThread.RunJobs();
        Assert.True(picker.FindControl<Button>("OptionMedium")!.IsFocused);

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(picker.IsOpen);
        Assert.Equal(TaskPriority.Low, Assert.Single(commits).Priority);
    }

    [AvaloniaFact]
    public void Tag_ReflectsPriority()
    {
        var (_, picker, _) = Mount(TaskPriority.High);
        var text = picker.FindControl<TextBlock>("TriggerText")!;
        var tag = picker.FindControl<Border>("TriggerTag")!;

        Assert.Equal("P1", text.Text);
        Assert.Contains("P1", tag.Classes);

        picker.Priority = TaskPriority.Low;

        Assert.Equal("P3", text.Text);
        Assert.Contains("P3", tag.Classes);
        Assert.DoesNotContain("P1", tag.Classes);
    }
}
