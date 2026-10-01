using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Appearance;
using FlowTask.Desktop.Services;
using FlowTask.Desktop.ViewModels;
using FlowTask.Desktop.Views;
using FlowTask.Infrastructure.Persistence;
using FlowTask.Infrastructure.Time;

namespace FlowTask.Desktop;

/// <summary>
/// 应用入口：组装仓储、视图模型、主视窗、托盘与退出旗标。
/// </summary>
public partial class App : Application
{
    private GlobalHotkeyService? _hotkeyService;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _toggleMiniMenuItem;
    private MainWindow? _mainWindow;

    /// <summary>
    /// 正在走彻底退出。为 true 时主窗/小窗的 Closing 不再 Cancel，
    /// 否则小窗 Hide 复用会把进程钉死（spec-close-to-tray）。
    /// </summary>
    public bool IsExiting { get; private set; }

    /// <summary>当前 App 实例；非桌面生命周期下为 null。</summary>
    public static App? CurrentApp => Current as App;

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            UnhandledExceptionGuard.Install();

            // 托盘 Hide 主窗后窗口仍存在；退出必须显式 Shutdown，不能靠「最后一个窗口关完」。
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (desktop is IActivatableLifetime activatable)
            {
                activatable.Activated += OnApplicationActivated;
            }

            if (OperatingSystem.IsMacOS())
            {
                MacOSApplicationIcon.Apply();
            }

            // 唯一允许触达系统时钟的实现，其余组件一律经 IClock 获取时间（Article 9）
            IClock clock = new SystemClock();

            // 三个仓储经 DatabaseLocation 解析同一文件；sqlite-net 按路径池化连接。
            ITaskRepository repository = new SqliteTaskRepository(clock);

            // 项目仓储须与任务仓储指向同一数据库文件：
            // 删除项目要在单事务内同时改动 Projects 与 Tasks 两张表，跨连接无法保证原子性。
            IProjectRepository projectRepository = new SqliteProjectRepository();
            IAppSettingsRepository settingsRepository = new SqliteAppSettingsRepository();

            var mainVm = new MainViewModel(repository, projectRepository, clock, settingsRepository);
            var quickCaptureVm = new QuickCaptureViewModel(
                repository, projectRepository, settingsRepository, clock);

            mainVm.RequestHideToTray += HideMainToTray;
            mainVm.RequestExitApplication += RequestExit;
            SingleInstanceGuard.Current?.ListenForReplacement(
                () => Dispatcher.UIThread.Post(RequestExit));

            desktop.Exit += (_, _) =>
            {
                _hotkeyService?.Dispose();
                _hotkeyService = null;
                if (_trayIcon is not null)
                {
                    _trayIcon.IsVisible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }
            };

            // 主窗 Show 之前必须先把已保存风格写进主题字典。挂在 Opened 上时窗口已经可见。
            LoggedTasks.FireAndForget(
                ShowMainWindowAsync(desktop, mainVm, quickCaptureVm),
                "ShowMainWindow");
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 读出已保存外观后再构造并显示主窗，避免首帧画出编译期默认风格。
    /// </summary>
    private async Task ShowMainWindowAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainViewModel mainVm,
        QuickCaptureViewModel quickCaptureVm)
    {
        try
        {
            await mainVm.LoadAppearanceAsync();
        }
        catch (Exception ex)
        {
            AppLog.Write("LoadAppearanceAsync", ex);
        }

        var mainWindow = new MainWindow(mainVm, quickCaptureVm);
        _mainWindow = mainWindow;
        AppearanceCoordinator.ApplyMaterial(mainWindow, mainVm.SelectedMaterial.Id);
        mainWindow.QuickCaptureVisibilityChanged += SyncTrayMiniMenuHeader;

        // Windows：进程级热键（主窗非前台亦可）；失败则保留主窗内 KeyDown 回退
        _hotkeyService = new GlobalHotkeyService(() =>
            Dispatcher.UIThread.Post(mainWindow.ToggleQuickCaptureFromHotkey));

        // 注册成功后必须关闭窗内 Alt+Space 监听，否则同一次按键会被系统级热键与窗内
        // KeyDown 两条路径分别触发一次 Toggle（见 MainWindow._systemHotkeyActive 注释）
        mainWindow.SetSystemHotkeyActive(_hotkeyService.TryStart());

        InstallTrayIcon();

        desktop.MainWindow = mainWindow;
        if (!mainWindow.IsVisible)
        {
            mainWindow.Show();
        }
    }

    /// <summary>彻底退出：置旗标后 Shutdown，主窗与小窗 Closing 放行。</summary>
    public void RequestExit()
    {
        if (IsExiting)
        {
            return;
        }

        IsExiting = true;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <summary>主窗藏进托盘：Hide 而非 Close，进程与热键继续。</summary>
    public void HideMainToTray()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.ShowInTaskbar = false;
        _mainWindow.Hide();
    }

    /// <summary>托盘左键：显示并激活主窗。</summary>
    public void RestoreMainFromTray()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.ShowInTaskbar = true;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnApplicationActivated(object? sender, ActivatedEventArgs e)
    {
        if (e.Kind == ActivationKind.Reopen)
        {
            RestoreMainFromTray();
        }
    }

    private void InstallTrayIcon()
    {
        _toggleMiniMenuItem = new NativeMenuItem("显示小窗");
        _toggleMiniMenuItem.Click += (_, _) => _mainWindow?.ToggleQuickCaptureFromHotkey();

        var exitItem = new NativeMenuItem("退出");
        exitItem.Click += (_, _) => RequestExit();

        var menu = new NativeMenu();
        menu.Items.Add(_toggleMiniMenuItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(
                AssetLoader.Open(new Uri("avares://FlowTask.Desktop/Assets/Brand/flowtask-icon.ico"))),
            ToolTipText = "FlowTask",
            Menu = menu,
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => RestoreMainFromTray();

        var icons = new TrayIcons { _trayIcon };
        TrayIcon.SetIcons(this, icons);
    }

    private void SyncTrayMiniMenuHeader()
    {
        if (_toggleMiniMenuItem is null || _mainWindow is null)
        {
            return;
        }

        _toggleMiniMenuItem.Header = _mainWindow.IsQuickCaptureVisible ? "隐藏小窗" : "显示小窗";
    }
}
