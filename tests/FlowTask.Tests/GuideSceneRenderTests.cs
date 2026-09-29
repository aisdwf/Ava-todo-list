using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlowTask.Desktop.ViewModels;
using FlowTask.Desktop.Views.Guide;
using Xunit;

namespace FlowTask.Tests;

/// <summary>
/// 11 个矢量场景能在 headless 窗口中构建、播完一整轮并复位且不抛异常（spec-onboarding-guide）。
/// </summary>
/// <remarks>
/// 场景脚本全在运行时按坐标拼装，编译器查不出过渡结构不匹配、资源键拼错这类错误；
/// 一旦在 DispatcherTimer 回调里抛出，会直接逃逸到进程顶层（参照水波纹 RenderTransform 闪退事故）。
/// </remarks>
public class GuideSceneRenderTests
{
    public static IEnumerable<object[]> Scenes()
        => Enum.GetValues<GuideSceneKind>().Select(kind => new object[] { kind });

    [AvaloniaTheory]
    [MemberData(nameof(Scenes))]
    public void Scene_BuildsAndPlaysFullLoop(GuideSceneKind kind)
    {
        var host = new GuideSceneHost { Width = 320, Height = 180, Scene = kind };
        var window = new Window { Width = 400, Height = 260, Content = host };
        window.Show();

        Assert.IsType<Viewbox>(host.Child);
        Assert.True(host.StepCount > 0);

        // 整轮 + 一次循环复位
        for (var i = 0; i <= host.StepCount; i++)
        {
            host.StepOnce();
            Dispatcher.UIThread.RunJobs();
        }

        // 测试宿主未启用 Skia（TestAppBuilder），不能截帧；强制一次布局以走完测量与排列
        window.UpdateLayout();
        Assert.True(host.Bounds.Width > 0);
        window.Close();
        Assert.Null(host.Child);
    }
}
