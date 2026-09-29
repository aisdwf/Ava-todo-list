namespace FlowTask.Desktop.ViewModels;

/// <summary>
/// 操作指南里每个条目对应的矢量动效场景（spec-onboarding-guide）。
/// </summary>
/// <remarks>
/// 场景是纯展示：枚举只标识"演示哪件事"，具体绘制归视图层
/// （<c>Views/Guide/GuideSceneHost</c>），ViewModel 不引用任何控件。
/// </remarks>
public enum GuideSceneKind
{
    /// <summary>输入标题、回车落入列表。</summary>
    AddTask,

    /// <summary>数字速记到期日与 P1–P3。</summary>
    DueAndPriority,

    /// <summary>圆环完成与点击标题展开编辑。</summary>
    EditAndComplete,

    /// <summary>悬停出现删除按钮并删除任务。</summary>
    DeleteTask,

    /// <summary>新建、选中、双击重命名项目。</summary>
    Projects,

    /// <summary>删除项目与级联确认条。</summary>
    DeleteProject,

    /// <summary>全局快捷键呼出小窗。</summary>
    QuickCapture,

    /// <summary>小窗内 Ctrl+Tab / Enter / Esc。</summary>
    QuickWindowKeys,

    /// <summary>默认到期天数与「启用默认到期」。</summary>
    DefaultDue,

    /// <summary>关窗进托盘与托盘菜单。</summary>
    CloseToTray,

    /// <summary>昼夜水波纹、齿轮与「?」入口。</summary>
    Theme
}

/// <summary>
/// 聚光灯引导可指向的界面锚点。XAML 以 <c>{x:Static}</c> 引用同一常量，
/// 目录与视图不各写一份字符串（Article 6）。
/// </summary>
public static class OnboardingTargets
{
    /// <summary>顶部新任务输入框。</summary>
    public const string AddInput = "AddInput";

    /// <summary>整条添加栏（含到期与优先级）。</summary>
    public const string AddBar = "AddBar";

    /// <summary>任务流中的首个任务行。</summary>
    public const string TaskRow = "TaskRow";

    /// <summary>任务流为空时的空状态区。</summary>
    public const string TaskEmpty = "TaskEmpty";

    /// <summary>侧栏视图与项目区。</summary>
    public const string SidebarProjects = "SidebarProjects";

    /// <summary>侧栏底部快捷小窗入口。</summary>
    public const string CaptureTrigger = "CaptureTrigger";

    /// <summary>右上角「? | 齿轮 | 昼夜」按钮组。</summary>
    public const string HeaderActions = "HeaderActions";
}

/// <summary>
/// 操作指南的一个条目。带 <see cref="TargetKeys"/> 的条目同时是首次引导的一步。
/// </summary>
/// <param name="Number">两位序号，列表展示用。</param>
/// <param name="Scene">配套动效场景。</param>
/// <param name="Group">分组名。</param>
/// <param name="Title">标题。</param>
/// <param name="Summary">一句话说明，引导气泡与指南页共用。</param>
/// <param name="Points">指南页补充要点。</param>
/// <param name="TargetKeys">聚光灯锚点，按顺序取第一个可见者；空表示不进首次引导。</param>
public sealed record GuideTopic(
    string Number,
    GuideSceneKind Scene,
    string Group,
    string Title,
    string Summary,
    IReadOnlyList<string> Points,
    IReadOnlyList<string> TargetKeys)
{
    /// <summary>列表上方微标：「01 · 基础」。</summary>
    public string Caption => $"{Number} · {Group}";

    /// <summary>是否属于首次聚光灯引导。</summary>
    public bool IsOnboardingStep => TargetKeys.Count > 0;
}

/// <summary>
/// 操作指南与首次引导的唯一文案来源。引导步骤从指南条目中筛出，不另写一份（Article 6）。
/// </summary>
public static class GuideCatalog
{
    private static readonly string[] NoTarget = Array.Empty<string>();

    /// <summary>指南页全部条目，按学习顺序排列。</summary>
    public static IReadOnlyList<GuideTopic> Topics { get; } = Number(new[]
    {
        new GuideTopic(string.Empty, GuideSceneKind.AddTask, "基础",
            "写下一件事，按回车",
            "顶部输入框随时待命。写完按回车，任务就会落进下方列表。",
            new[]
            {
                "当前选中某个项目时，新任务自动归入该项目。",
                "标题留空时按回车不会创建任务。"
            },
            new[] { OnboardingTargets.AddInput }),

        new GuideTopic(string.Empty, GuideSceneKind.DueAndPriority, "基础",
            "到期日与优先级",
            "输入框右侧点「到期」弹出日期框，旁边的 P1–P3 一键定优先级。",
            new[]
            {
                "日期框里敲数字预览，按回车生效：10 = 本月 10 日；0310 = 今年 3 月 10 日；20260310 = 完整日期。",
                "也可以直接点日历上的某一天，或点「默认 +N 天」「清除」，一下就生效。",
                "列表里点任务标题下方的日期（或「到期」）可以随时改期；已完成的任务显示完成日。"
            },
            new[] { OnboardingTargets.AddBar }),

        new GuideTopic(string.Empty, GuideSceneKind.EditAndComplete, "基础",
            "勾选完成，点标题编辑",
            "点左侧圆环完成任务，它会划线并沉到底部；点标题展开编辑面板。",
            new[]
            {
                "再点一次圆环即可撤销完成。",
                "编辑面板可改标题、优先级和所属项目，修改即时保存。"
            },
            new[] { OnboardingTargets.TaskRow, OnboardingTargets.TaskEmpty }),

        new GuideTopic(string.Empty, GuideSceneKind.DeleteTask, "基础",
            "删除任务",
            "鼠标悬停任务行，右侧才会浮现 ×，点它即可删除。删除不可撤销。",
            new[]
            {
                "只是暂时不做时，勾选完成比删除更稳妥。"
            },
            NoTarget),

        new GuideTopic(string.Empty, GuideSceneKind.Projects, "项目",
            "用项目归类",
            "侧栏「新建项目」创建分类；单击项目只看它的任务，双击项目名可以重命名。",
            new[]
            {
                "不想分类时，任务会落入 Default 项目。",
                "在某个项目里新建的任务，自动归入这个项目。"
            },
            new[] { OnboardingTargets.SidebarProjects }),

        new GuideTopic(string.Empty, GuideSceneKind.DeleteProject, "项目",
            "删除项目",
            "悬停项目行点垃圾桶，确认框会写明将一并删除多少条任务。",
            new[]
            {
                "Default 项目不能删除。",
                "点「确认删除」之前都可以取消：点「取消」、点框外或按 Esc。"
            },
            NoTarget),

        new GuideTopic(string.Empty, GuideSceneKind.QuickCapture, "小窗",
            "快捷小窗",
            "按侧栏键帽上的快捷键，随时呼出小窗查看、勾选、随手记，主窗不必在前台。",
            new[]
            {
                "主窗藏进托盘后，快捷键依然有效。",
                "也可以直接点侧栏底部的「快捷小窗」。"
            },
            new[] { OnboardingTargets.CaptureTrigger }),

        new GuideTopic(string.Empty, GuideSceneKind.QuickWindowKeys, "小窗",
            "小窗键位",
            "小窗全程不用鼠标：Ctrl+Tab 切换项目，Ctrl+D 设到期日，↵ 保存，Esc 收起。",
            new[]
            {
                "Ctrl+Shift+Tab 反向切换项目。",
                "新任务归入小窗当前选中的项目；保存后优先级保持不变，方便连续记同档任务。",
                "底栏的日期按钮和任务右侧的日期也可以直接点击设置。"
            },
            NoTarget),

        new GuideTopic(string.Empty, GuideSceneKind.DefaultDue, "设置",
            "默认到期天数",
            "在设置 → 通用里定好天数，之后在日期框里点「默认 +N 天」就是今天加这么多天。",
            new[]
            {
                "范围 1–30 天，点「保存」后生效。"
            },
            NoTarget),

        new GuideTopic(string.Empty, GuideSceneKind.CloseToTray, "设置",
            "关闭与托盘",
            "点窗口 × 可以藏进托盘，进程和快捷键继续工作；没设默认时会先问你。",
            new[]
            {
                "托盘左键打开主窗，右键可以显隐小窗或彻底退出。",
                "设置 → 通用可以随时改成「彻底退出」。"
            },
            NoTarget),

        new GuideTopic(string.Empty, GuideSceneKind.Theme, "设置",
            "主题、设置与指南",
            "右上角切换昼夜，齿轮进入设置；不确定怎么用时，点「?」打开操作指南。",
            new[]
            {
                "设置 → 外观可以换主题色板与窗口材质。",
                "操作指南里可以随时重新播放新手引导。"
            },
            new[] { OnboardingTargets.HeaderActions })
    });

    /// <summary>首次聚光灯引导的步骤：指南条目中带锚点者，保持指南顺序。</summary>
    public static IReadOnlyList<GuideTopic> OnboardingSteps { get; } =
        Topics.Where(topic => topic.IsOnboardingStep).ToArray();

    private static IReadOnlyList<GuideTopic> Number(IReadOnlyList<GuideTopic> topics)
        => topics.Select((topic, index) => topic with { Number = (index + 1).ToString("00") }).ToArray();
}
