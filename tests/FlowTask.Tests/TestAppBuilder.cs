using Avalonia;
using Avalonia.Headless;
using FlowTask.Desktop;

[assembly: AvaloniaTestApplication(typeof(FlowTask.Tests.TestAppBuilder))]

namespace FlowTask.Tests;

/// <summary>
/// 为需要 Avalonia 运行时的测试提供 headless 宿主。
/// </summary>
/// <remarks>
/// 主题令牌解析、笔刷构造与过渡行为都要求真实的 <see cref="Application"/> 实例
/// 且必须在 Avalonia UI 线程上访问。缺少此宿主时，资源字典相关缺陷无法被
/// 自动化测试捕获 —— 这正是「外观切换无变化」与「昼夜切换闪退」两个故障
/// 得以通过全部机器门禁的结构性原因。
///
/// 使用此宿主的测试须以 <c>[AvaloniaFact]</c> / <c>[AvaloniaTheory]</c> 标注，
/// 框架会自动切换到 UI 线程，无需手工调度。
/// </remarks>
public static class TestAppBuilder
{
    /// <summary>
    /// 构建 headless 测试应用。
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
