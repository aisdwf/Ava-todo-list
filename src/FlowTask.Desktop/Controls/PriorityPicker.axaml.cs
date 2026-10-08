using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using FlowTask.Core.Enums;
using FlowTask.Desktop.Converters;
using FlowTask.Desktop.ViewModels;

namespace FlowTask.Desktop.Controls;

/// <summary>
/// 任务行优先级选择器：P 标签作为触发入口 + 锚定在标签下方的浮层（spec-inline-task-edit）。
/// </summary>
/// <remarks>
/// <para>
/// 用户原话（2026-09-29）：「优先级，日期我觉得用类似目前日期的方式，小窗或者比较合适的UI效果来修改比较好」。
/// 故交互与 <see cref="DueDatePicker"/> 一致：点入口开浮层，点选即提交关闭，Esc / 点外部取消。
/// </para>
/// <para>
/// 选项是固定的三档，没有需要跨实例共享的编辑状态，因此不像 <see cref="DueDatePicker"/> 那样注入编辑器，
/// 只在提交时以 <see cref="PriorityCommit"/> 为参数执行 <see cref="CommitCommand"/>。
/// </para>
/// </remarks>
public partial class PriorityPicker : UserControl
{
    /// <summary>当前优先级；决定标签文案与配色。</summary>
    public static readonly StyledProperty<TaskPriority> PriorityProperty =
        AvaloniaProperty.Register<PriorityPicker, TaskPriority>(nameof(Priority), TaskPriority.Medium);

    /// <summary>点选后执行的命令，参数为 <see cref="PriorityCommit"/>。</summary>
    public static readonly StyledProperty<ICommand?> CommitCommandProperty =
        AvaloniaProperty.Register<PriorityPicker, ICommand?>(nameof(CommitCommand));

    /// <summary>随 <see cref="PriorityCommit"/> 一并传出的目标（任务行）。</summary>
    public static readonly StyledProperty<object?> CommitTargetProperty =
        AvaloniaProperty.Register<PriorityPicker, object?>(nameof(CommitTarget));

    private static readonly TransformOperations HiddenOffset = TransformOperations.Parse("translateY(-6px)");
    private static readonly TransformOperations RestOffset = TransformOperations.Parse("none");

    public PriorityPicker()
    {
        InitializeComponent();

        Trigger.Click += (_, _) => Open();
        PickerPopup.Opened += OnPopupOpened;
        PickerPopup.Closed += OnPopupClosed;

        foreach (var option in new[] { OptionHigh, OptionMedium, OptionLow })
        {
            option.Click += OnOptionClick;
        }

        Surface.AddHandler(KeyDownEvent, OnSurfaceKeyDown, RoutingStrategies.Tunnel);
        UpdateTrigger();
    }

    public TaskPriority Priority
    {
        get => GetValue(PriorityProperty);
        set => SetValue(PriorityProperty, value);
    }

    public ICommand? CommitCommand
    {
        get => GetValue(CommitCommandProperty);
        set => SetValue(CommitCommandProperty, value);
    }

    public object? CommitTarget
    {
        get => GetValue(CommitTargetProperty);
        set => SetValue(CommitTargetProperty, value);
    }

    /// <summary>浮层是否打开。</summary>
    public bool IsOpen => PickerPopup.IsOpen;

    /// <summary>打开浮层。</summary>
    public void Open()
    {
        if (PickerPopup.IsOpen)
        {
            return;
        }

        foreach (var option in new[] { OptionHigh, OptionMedium, OptionLow })
        {
            option.Classes.Set("Current", OptionOf(option) == Priority);
        }

        PickerPopup.IsOpen = true;
    }

    /// <summary>不提交地关闭浮层。</summary>
    public void Close() => PickerPopup.IsOpen = false;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PriorityProperty)
        {
            UpdateTrigger();
        }
    }

    /// <summary>
    /// 标签文案与配色类复用 <see cref="PriorityTagConverter"/> 与既有 <c>PriorityTag.P1–P3</c> 样式（Article 6）。
    /// </summary>
    private void UpdateTrigger()
    {
        var priority = Priority;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        TriggerText.Text = PriorityTagConverter.Tag.Convert(priority, typeof(string), null, culture) as string;
        TriggerTag.Classes.Set("P1", priority == TaskPriority.High);
        TriggerTag.Classes.Set("P2", priority == TaskPriority.Medium);
        TriggerTag.Classes.Set("P3", priority == TaskPriority.Low);

        var name = PriorityTagConverter.DisplayName.Convert(priority, typeof(string), null, culture) as string;
        ToolTip.SetTip(Trigger, $"{name}，点击修改");
        AutomationProperties.SetName(Trigger, $"{name}，点击修改");
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        // 先落到隐藏态再在下一帧放开，过渡才有起点（同 DueDatePicker）
        Dispatcher.UIThread.Post(() =>
        {
            Surface.Opacity = 1;
            Surface.RenderTransform = RestOffset;
            CurrentOption().Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Loaded);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        // 复位到隐藏态时不播放过渡，保证下次打开从起点开始
        var transitions = Surface.Transitions;
        Surface.Transitions = null;
        Surface.Opacity = 0;
        Surface.RenderTransform = HiddenOffset;
        Surface.Transitions = transitions;
    }

    private void OnOptionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button option || OptionOf(option) is not { } picked)
        {
            return;
        }

        var target = CommitTarget;
        var command = CommitCommand;
        Close();

        var commit = new PriorityCommit(target, picked);
        if (command?.CanExecute(commit) == true)
        {
            command.Execute(commit);
        }
    }

    /// <summary>浮层内键盘：Esc 关闭；上下方向键在三档间移动焦点，Enter / Space 由按钮自身激活。</summary>
    private void OnSurfaceKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Up:
                MoveFocus(e.Key == Key.Down ? 1 : -1);
                e.Handled = true;
                break;
        }
    }

    private void MoveFocus(int step)
    {
        var options = new[] { OptionHigh, OptionMedium, OptionLow };
        var index = Array.FindIndex(options, o => o.IsFocused);
        var next = index < 0 ? 0 : (index + step + options.Length) % options.Length;
        options[next].Focus(NavigationMethod.Directional);
    }

    private Button CurrentOption() => Priority switch
    {
        TaskPriority.High => OptionHigh,
        TaskPriority.Low => OptionLow,
        _ => OptionMedium
    };

    private static TaskPriority? OptionOf(Button option)
        => option.Tag is string name && Enum.TryParse<TaskPriority>(name, out var priority) ? priority : null;
}
