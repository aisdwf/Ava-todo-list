using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlowTask.Core.Enums;
using FlowTask.Core.Models;
using FlowTask.Desktop.Appearance;
using FlowTask.Desktop.ViewModels;
using FlowTask.Desktop.Views;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 修饰色绑定主题的回归防护（spec-theme-bound-decoration-colors）。
/// </summary>
/// <remarks>
/// 两类缺陷都能通过编译与既有测试，只在真实界面上暴露：
/// 1. 修饰色取自库里的字面 hex，换主题不变（固定紫 / 绿）；
/// 2. 引用未定义的 <c>DynamicResource</c> 键，Avalonia 静默跳过，控件退回默认色
///    （<c>DangerBrush</c> / <c>SurfaceBrush</c> 曾因此失效）。
/// </remarks>
public class DecorationColorTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"flowtask_decor_{Guid.NewGuid():N}.db");
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

    /// <summary>
    /// 每个 axaml 里引用的 <c>*Brush</c> / <c>*Color</c> 键都必须在应用资源中解析得到。
    /// </summary>
    [AvaloniaFact]
    public void EveryDynamicColorKeyInXaml_IsDefined()
    {
        var root = Path.Combine(RepoRoot(), "src", "FlowTask.Desktop");
        var pattern = new Regex(@"\{DynamicResource\s+([A-Za-z0-9_]+(?:Brush|Color))\s*\}", RegexOptions.CultureInvariant);

        var keys = Directory.GetFiles(root, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(path => pattern.Matches(File.ReadAllText(path)).Select(m => (Key: m.Groups[1].Value, File: Path.GetFileName(path))))
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);

        var app = Application.Current!;
        var missing = keys
            .Where(entry => !IsResolvable(app, entry.Key, ThemeVariant.Dark) || !IsResolvable(app, entry.Key, ThemeVariant.Light))
            .Select(entry => $"{entry.Key} ({entry.File})")
            .ToList();

        Assert.True(missing.Count == 0, "Undefined DynamicResource keys resolve silently to nothing: " + string.Join(", ", missing));
    }

    /// <summary>
    /// 任务行色条与项目色点在真实主窗中取主题色：P1 语义红、P2 强调色、P3 强调色光晕；
    /// 换主题后色条与色点同步变化。
    /// </summary>
    [AvaloniaFact]
    public async Task TaskRowStripeAndProjectDot_FollowPriorityAndTheme()
    {
        var tasks = new SqliteTaskRepository(_clock, _dbPath);
        var projects = new SqliteProjectRepository(_dbPath);
        var settings = new SqliteAppSettingsRepository(_dbPath);
        var vm = new MainViewModel(tasks, projects, _clock, settings);
        await vm.InitializeAsync();

        foreach (var (title, priority) in new[] { ("高", TaskPriority.High), ("中", TaskPriority.Medium), ("低", TaskPriority.Low) })
        {
            await tasks.SaveTaskAsync(TaskItemFactory.Create(_clock, title, priority, DefaultProject.Id));
        }

        await vm.InitializeAsync();
        var window = new MainWindow(vm, new QuickCaptureViewModel(tasks, projects, settings, _clock)) { Width = 1180, Height = 780 };
        window.Show();

        try
        {
            foreach (var presetId in new[] { "rose-garden", "forest-whisper" })
            {
                AppearanceCoordinator.ApplyThemePreset(presetId);
                Flush(window);

                var accent = Color.Parse(AppearanceCoordinator.FindThemePreset(presetId).Dark.AccentHex);
                Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);

                var stripes = window.GetVisualDescendants().OfType<Border>()
                    .Where(border => border.Classes.Contains("PriorityStripe"))
                    .ToDictionary(border => ((TaskRowViewModel)border.DataContext!).Task.Priority);
                Assert.Equal(3, stripes.Count);

                var high = Resolve(window, "PriorityHighBrush");
                Assert.Equal(high.Color, BrushOf(stripes[TaskPriority.High]).Color);

                var medium = BrushOf(stripes[TaskPriority.Medium]);
                Assert.Equal(accent, medium.Color);
                Assert.Equal(1.0, medium.Opacity);

                // P3 与 P2 同色相、更弱：靠不透明度拉开梯度
                var low = BrushOf(stripes[TaskPriority.Low]);
                Assert.Equal(accent, low.Color);
                Assert.True(low.Opacity < medium.Opacity);

                var dots = window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>()
                    .Where(dot => dot.Classes.Contains("ProjectDot") && dot.IsEffectivelyVisible)
                    .ToList();
                Assert.NotEmpty(dots);
                Assert.All(dots, dot => Assert.Equal(accent, Assert.IsAssignableFrom<ISolidColorBrush>(dot.Fill).Color));
            }
        }
        finally
        {
            AppearanceCoordinator.ApplyThemePreset("default");
            window.Close();
        }
    }

    private static ISolidColorBrush BrushOf(Border border)
        => Assert.IsAssignableFrom<ISolidColorBrush>(border.Background);

    private static ISolidColorBrush Resolve(Window window, string key)
    {
        Assert.True(window.TryFindResource(key, window.ActualThemeVariant, out var value), $"{key} missing");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value);
    }

    private static bool IsResolvable(Application app, string key, ThemeVariant variant)
        => app.TryGetResource(key, variant, out var value) && value is not null;

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
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
