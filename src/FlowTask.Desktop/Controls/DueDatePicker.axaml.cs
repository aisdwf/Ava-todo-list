using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlowTask.Desktop.Converters;
using FlowTask.Desktop.ViewModels;

namespace FlowTask.Desktop.Controls;

/// <summary>
/// 到期日选择器：一个可点击的触发入口 + 锚定在入口下方的浮层（spec-due-date-picker）。
/// 主窗创建栏、主窗任务行、小窗任务行与小窗底栏共用本控件，避免多份手抄 XAML。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么 <see cref="Editor"/> 由外部注入而非每个实例自建</b>：任务行成百上千，
/// 同一时刻只可能有一个浮层打开，故一个窗口共享一个编辑器即可。
/// 共享意味着 <see cref="DueDateEditorViewModel.Committed"/> 会被所有实例看到，
/// 因此只在本实例浮层打开期间订阅。
/// </para>
/// <para>
/// <b>提交语义</b>：创建栏不设 <see cref="CommitCommand"/>，只依赖编辑器自身的
/// <see cref="DueDateEditorViewModel.SelectedDate"/>；任务行设置 <see cref="CommitCommand"/>，
/// 以 <see cref="DueDateCommit"/> 为参数交给持有者落库。
/// </para>
/// </remarks>
public partial class DueDatePicker : UserControl
{
    /// <summary>当前展示的到期日；决定触发入口的文案与颜色。</summary>
    public static readonly StyledProperty<DateTime?> DateProperty =
        AvaloniaProperty.Register<DueDatePicker, DateTime?>(nameof(Date));

    /// <summary>浮层使用的编辑器。</summary>
    public static readonly StyledProperty<DueDateEditorViewModel?> EditorProperty =
        AvaloniaProperty.Register<DueDatePicker, DueDateEditorViewModel?>(nameof(Editor));

    /// <summary>提交后执行的命令，参数为 <see cref="DueDateCommit"/>。</summary>
    public static readonly StyledProperty<ICommand?> CommitCommandProperty =
        AvaloniaProperty.Register<DueDatePicker, ICommand?>(nameof(CommitCommand));

    /// <summary>提交时随 <see cref="DueDateCommit"/> 一并传出的目标（通常是任务行）。</summary>
    public static readonly StyledProperty<object?> CommitTargetProperty =
        AvaloniaProperty.Register<DueDatePicker, object?>(nameof(CommitTarget));

    /// <summary>无到期日时触发入口的占位文案。</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<DueDatePicker, string>(nameof(Placeholder), "到期");

    private static readonly TransformOperations HiddenOffset = TransformOperations.Parse("translateY(-6px)");
    private static readonly TransformOperations RestOffset = TransformOperations.Parse("none");

    private DueDateEditorViewModel? _activeEditor;

    public DueDatePicker()
    {
        InitializeComponent();

        Trigger.Click += (_, _) => Open();
        PickerPopup.Opened += OnPopupOpened;
        PickerPopup.Closed += OnPopupClosed;

        // Tunnel：数字框与日历都会自行消费部分按键，Enter / Esc 须在它们之前拦下
        Surface.AddHandler(KeyDownEvent, OnSurfaceKeyDown, RoutingStrategies.Tunnel);

        // 日历按下即选中、方向键也会改选中；只有「指针点在某一天上」才算提交。
        // handledEventsToo：CalendarDayButton 会把指针事件标为已处理。
        DueCalendar.AddHandler(
            PointerReleasedEvent,
            OnCalendarPointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        UpdateTrigger();
    }

    public DateTime? Date
    {
        get => GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    public DueDateEditorViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
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

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// 浮层相对触发入口的弹出方向。主窗入口靠右（创建栏在优先级旁），
    /// 默认左对齐会让约 300px 宽的浮层越出窗口右缘，主窗改为右对齐向左展开。
    /// </summary>
    public PlacementMode PopupPlacement
    {
        get => PickerPopup.Placement;
        set => PickerPopup.Placement = value;
    }

    /// <summary>浮层是否打开。</summary>
    public bool IsOpen => PickerPopup.IsOpen;

    /// <summary>浮层关闭（无论提交还是取消）。宿主可据此归还焦点。</summary>
    public event EventHandler? PickerClosed;

    /// <summary>打开浮层，供点击与快捷键（小窗 Ctrl+D）共用。</summary>
    public void Open()
    {
        if (Editor is not { } editor || PickerPopup.IsOpen)
        {
            return;
        }

        editor.Load(Date);
        _activeEditor = editor;
        editor.Committed += OnEditorCommitted;
        Surface.DataContext = editor;
        PickerPopup.IsOpen = true;
    }

    /// <summary>不提交地关闭浮层。</summary>
    public void Close() => PickerPopup.IsOpen = false;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DateProperty || change.Property == PlaceholderProperty)
        {
            UpdateTrigger();
        }
    }

    /// <summary>
    /// 触发入口文案与状态类。相对文案与逾期/今天判断复用 <see cref="DueDateTextConverter"/> 等既有定义（Article 6）。
    /// </summary>
    private void UpdateTrigger()
    {
        var date = Date;
        TriggerText.Text = date is null
            ? Placeholder
            : DueDateTextConverter.Instance.Convert(date, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture) as string;

        var overdue = DueDateOverdueConverter.Instance.Convert(date, typeof(bool), null, System.Globalization.CultureInfo.CurrentCulture) is true;
        var today = DueDateTodayConverter.Instance.Convert(date, typeof(bool), null, System.Globalization.CultureInfo.CurrentCulture) is true;

        TriggerText.Classes.Set("Overdue", overdue);
        TriggerText.Classes.Set("DueToday", today);
        PseudoClasses.Set(":unset", date is null);
        ToolTip.SetTip(Trigger, date is null ? "设置到期日" : $"到期 {date:yyyy-MM-dd}，点击修改");
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        // 先落到隐藏态再在下一帧放开，过渡才有起点（同 MainWindow 水波纹的做法）
        Dispatcher.UIThread.Post(() =>
        {
            Surface.Opacity = 1;
            Surface.RenderTransform = RestOffset;
            FocusCalendarDay();
        }, DispatcherPriority.Loaded);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (_activeEditor is { } editor)
        {
            editor.Committed -= OnEditorCommitted;
        }

        _activeEditor = null;

        // 复位到隐藏态时不播放过渡，保证下次打开从起点开始
        var transitions = Surface.Transitions;
        Surface.Transitions = null;
        Surface.Opacity = 0;
        Surface.RenderTransform = HiddenOffset;
        Surface.Transitions = transitions;

        PickerClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnEditorCommitted(DateTime? date)
    {
        var target = CommitTarget;
        var command = CommitCommand;
        Close();

        var commit = new DueDateCommit(target, date);
        if (command?.CanExecute(commit) == true)
        {
            command.Execute(commit);
        }
    }

    private void OnSurfaceKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            // 只在日历内按 Enter 时提交高亮日；Tab 到预设按钮上按 Enter 应激活按钮本身
            case Key.Enter when IsWithin(e.Source as Visual, DueCalendar):
                _activeEditor?.CommitPreviewCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnCalendarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || !IsOnDayButton(e.Source as Visual))
        {
            return;
        }

        // 选中在按下/抬起的控件内部处理完后才写回绑定，延后一拍再读
        Dispatcher.UIThread.Post(() =>
        {
            if (_activeEditor is { CalendarDate: { } picked } editor)
            {
                editor.CommitDate(picked);
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// 把键盘焦点交给日历里的某一天：已选日优先，其次今天，再次本月首个可用日。
    /// 方向键移动高亮、Enter 提交，保留全键盘路径（R-1.4）。
    /// </summary>
    /// <remarks>
    /// <see cref="Calendar"/> 本身 <c>Focusable=false</c>，直接 <c>Focus()</c> 会静默失败，
    /// 可聚焦的是模板里的 <see cref="CalendarDayButton"/>。
    /// </remarks>
    private void FocusCalendarDay()
    {
        var days = DueCalendar.GetVisualDescendants()
            .OfType<CalendarDayButton>()
            .Where(b => b.IsEffectivelyVisible && b.IsEnabled)
            .ToList();

        // CalendarDayButton 的 IsSelected / IsToday 在 11.2 为 internal；
        // 每个日按钮的 DataContext 是它代表的 DateTime，据此匹配。
        static DateTime? DayOf(CalendarDayButton b) => b.DataContext is DateTime d ? d.Date : null;
        var wanted = (DueCalendar.SelectedDate ?? DueDateDisplay.Today()).Date;

        var target = days.FirstOrDefault(b => DayOf(b) == wanted)
                     ?? days.FirstOrDefault(b => DayOf(b) is { } d
                                                 && d.Month == DueCalendar.DisplayDate.Month
                                                 && d.Year == DueCalendar.DisplayDate.Year)
                     ?? days.FirstOrDefault();

        target?.Focus(NavigationMethod.Directional);
    }

    private static bool IsWithin(Visual? source, Visual container)
    {
        for (var node = source; node is not null; node = node.GetVisualParent())
        {
            if (ReferenceEquals(node, container))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOnDayButton(Visual? source)
    {
        for (var node = source; node is not null; node = node.GetVisualParent())
        {
            if (node is CalendarDayButton)
            {
                return true;
            }

            if (node is Calendar)
            {
                return false;
            }
        }

        return false;
    }
}
