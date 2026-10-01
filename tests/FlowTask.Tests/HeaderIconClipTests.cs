using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlowTask.Desktop.ViewModels;
using FlowTask.Desktop.Views;
using FlowTask.Infrastructure.Persistence;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 顶栏加粗图标不被 Viewbox 切边（spec-icon-refresh）。
/// </summary>
/// <remarks>
/// <c>Path.Stretch=Uniform</c> 只按几何的填充外框缩放，描边仍向外溢出半个线宽；
/// <c>Viewbox</c> 默认 <c>ClipToBounds=True</c>，会把齿顶、光芒末端与圆的最外沿切平。
/// 编译与其它测试都看不出这个问题，只在真实渲染时表现为「四周被切割」。
/// </remarks>
public class HeaderIconClipTests : IDisposable
{
    private readonly string _dbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"flowtask_icons_{Guid.NewGuid():N}.db");
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
    /// 每个加粗图标的描边外沿都超出其布局框，且承载它的 Viewbox 不裁剪。
    /// </summary>
    [AvaloniaFact]
    public async Task BoldHeaderIcons_StrokeOverflowsLayoutBox_AndViewboxDoesNotClip()
    {
        var tasks = new SqliteTaskRepository(_clock, _dbPath);
        var projects = new SqliteProjectRepository(_dbPath);
        var settings = new SqliteAppSettingsRepository(_dbPath);
        var vm = new MainViewModel(tasks, projects, _clock, settings);
        await vm.InitializeAsync();

        var window = new MainWindow(vm, new QuickCaptureViewModel(tasks, projects, settings, _clock)) { Width = 1180, Height = 780 };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var icons = window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
                .Where(path => path.Classes.Contains("Bold"))
                .ToList();

            // ? / 设置 / 太阳 / 月亮：昼夜两枚同处一格，显隐互斥，但都在可视树里
            Assert.Equal(4, icons.Count);

            foreach (var icon in icons)
            {
                var box = Assert.IsType<Viewbox>(icon.GetVisualAncestors().OfType<Viewbox>().First());
                Assert.False(box.ClipToBounds, "Header icon Viewbox clips the stroke edge; use Classes=\"HeaderIcon\".");
            }

            // 前提成立：已布局的图标描边确实溢出 Path 布局框，否则上面守护的问题不存在。
            // 隐藏的那一枚昼夜图标（及未进入布局的按钮）Bounds 为 0，不参与此项。
            var laidOut = icons.Where(icon => icon.Bounds.Width > 0).ToList();
            Assert.NotEmpty(laidOut);
            foreach (var icon in laidOut)
            {
                var pen = new Pen(Colors.Black.ToUInt32(), icon.StrokeThickness) { LineCap = icon.StrokeLineCap };
                var stroke = icon.RenderedGeometry!.GetRenderBounds(pen);
                Assert.True(stroke.X < 0 && stroke.Right > icon.Bounds.Width,
                    $"Expected stroke to overflow the layout box, got {stroke} in {icon.Bounds}");
            }
        }
        finally
        {
            window.Close();
        }
    }
}
