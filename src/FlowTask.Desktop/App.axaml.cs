using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FlowTask.Core.Interfaces;
using FlowTask.Desktop.Services;
using FlowTask.Desktop.ViewModels;
using FlowTask.Desktop.Views;
using FlowTask.Infrastructure.Persistence;
using FlowTask.Infrastructure.Time;

namespace FlowTask.Desktop;

/// <summary>
/// 应用入口：组装仓储、视图模型与主视窗。
/// </summary>
public partial class App : Application
{
    private GlobalHotkeyService? _hotkeyService;

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
            if (OperatingSystem.IsMacOS())
            {
                MacOSApplicationIcon.Apply();
            }

            // 唯一允许触达系统时钟的实现，其余组件一律经 IClock 获取时间（Article 9）
            IClock clock = new SystemClock();

            // 单一仓储实例供两个窗口共享，保证 SQLite 连接与初始化状态唯一
            ITaskRepository repository = new SqliteTaskRepository(clock);

            // 项目仓储须与任务仓储指向同一数据库文件：
            // 删除项目要在单事务内同时改动 Projects 与 Tasks 两张表，跨连接无法保证原子性。
            IProjectRepository projectRepository = new SqliteProjectRepository();
            ITagRepository tagRepository = new SqliteTagRepository(clock);
            IAppSettingsRepository settingsRepository = new SqliteAppSettingsRepository();

            var mainVm = new MainViewModel(repository, projectRepository, tagRepository, clock, settingsRepository);
            var quickCaptureVm = new QuickCaptureViewModel(
                repository, projectRepository, tagRepository, settingsRepository, clock);

            var mainWindow = new MainWindow(mainVm, quickCaptureVm);
            desktop.MainWindow = mainWindow;

            // Windows：进程级热键（主窗非前台亦可）；失败则保留主窗内 KeyDown 回退
            _hotkeyService = new GlobalHotkeyService(() =>
                Dispatcher.UIThread.Post(mainWindow.ToggleQuickCaptureFromHotkey));
            _ = _hotkeyService.TryStart();

            desktop.Exit += (_, _) =>
            {
                _hotkeyService?.Dispose();
                _hotkeyService = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
