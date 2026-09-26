# spec-unified-svg-icons: 统一描边图标并移除更换颜色

## Metadata

- **ID**: spec-unified-svg-icons
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-26
- **Last Updated**: 2026-09-26

---

## Why

主窗口与快捷小窗的操作图标是互不相关的 Unicode / emoji（`☀` 与 `☾`、`⚙`、`←`、`🗑`、`✕`、`＋`、`✦`）。它们落到不同字体回退上，线宽和视觉重量不一致。昼夜切换把这个问题暴露得最明显：深色主题的太阳和浅色主题的月亮像两套图标。

项目行上的「更换颜色」入口同样是一枚风格不符的字符（`◐`）。所有者确认该操作暂时没有价值，应删除入口，而不是把它重画成新图标。

## What

把纯操作图标收成同一套 24×24 描边几何，用 SVG 路径数据绘制，经 `Viewbox` 缩放到各按钮的光学尺寸。线宽 1.75、圆头、圆角连接。颜色只跟现有文字层级：顶栏用次级文字色，行内弱操作用三级文字色，快捷小窗星标用强调色。

昼夜切换仍由 `IsDarkTheme` 派生：深色显示太阳（提示可切到亮色），浅色显示月亮。不再经字符串转换器。

删除「更换颜色」按钮、`ChangeProjectColorCommand` 与只为它存在的色轮方法。`Project.ColorHex` 保留：新建项目仍按调色板取默认色，侧边栏色点与任务行色条继续读取该字段。

## Non-goals

- 不绘制图表，不新增统计视图。
- 不改应用品牌图标（`Assets/Brand`）。
- 不改快捷键字样（`↵`、`Esc`）。
- 不提供按图标单独换色的能力。
- 不改项目色的存储、默认取色，以及色点 / 色条的展示。

## Constraints and decisions

- 所有者 2026-09-26 确认范围，并明确删除「更换颜色」。
- 图标几何只有一份资源字典，避免每个按钮各写一套路径（Article 6）。
- 太阳 / 月亮的显隐直接绑定 `IsDarkTheme`，不另存图标状态（Article 10）。
- 视觉约束写入 `docs/design/design-visual-language.md`；本 SPEC 只记录这次改动的进度。
- 历史 SPEC（`spec-editorial-and-ripple-theme`、`spec-viewmodel-command-decomposition`）保留当时的实现记录，不回改。

## Acceptance criteria

- [x] 昼夜、设置、返回、项目删除、任务删除、新建项目、快捷小窗星标使用同一描边几何，不再使用上述字符。
- [x] 深色主题显示太阳，浅色主题显示月亮，二者线宽一致。所有者 2026-09-26 看过预览包，确认没问题。
- [x] 项目行不再有「更换颜色」入口；创建项目仍写入 `ColorHex`，色点与任务色条仍显示。
- [x] `dotnet build` 0 警告 0 错误；`dotnet test` 不回退。2026-09-26：179 通过，0 失败。

## Staged plan

1. 建立共享描边图标资源与样式。
2. 替换主窗口与快捷小窗中的字符图标。
3. 删除更换颜色的命令、操作类、色轮方法与对应测试。
4. 同步视觉语言文档与本索引，并跑构建和测试。

## Change checklist

- [x] `src/FlowTask.Desktop/Styles/Icons.axaml`
- [x] `src/FlowTask.Desktop/Styles/EditorialStyles.axaml`
- [x] `src/FlowTask.Desktop/App.axaml`
- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml`
- [x] `src/FlowTask.Desktop/Views/QuickCaptureWindow.axaml`
- [x] 删除 `Converters/ThemeIconConverter.cs`
- [x] 删除 `ViewModels/Actions/ChangeProjectColorViewModel.cs`
- [x] `ViewModels/MainViewModel.cs`
- [x] `Appearance/AppearanceCoordinator.cs`
- [x] `tests/FlowTask.Tests/ConverterTests.cs`
- [x] `tests/FlowTask.Tests/ProjectInteractionTests.cs`
- [x] `tests/FlowTask.Tests/AppearanceCoordinatorTests.cs`
- [x] `docs/design/design-visual-language.md`
- [x] `docs/specs/README.md`

## Progress log

### 2026-09-26

- Completed: 所有者确认范围；工作树 `feature/unified-svg-icons` 从 `origin/main`（`2ccba53`）创建。图标资源、界面替换、更换颜色删除均已落地。`dotnet test` 179 通过。
- Decisions: 删除更换颜色入口；保留 `ColorHex` 展示；图表与品牌图标不在范围内。
- Current resume point: 所有者已用预览包确认视觉，本 SPEC 关闭。
- Subagent/task references, when used: None.

## Verification

- Automated: 2026-09-26 `dotnet test FlowTask.sln --nologo -v q`，179 通过，0 失败。
- Manual: 2026-09-26 所有者打开 `preview/feature/unified-svg-icons/FlowTask.exe` 后回复「没什么问题」。
- Not run or not covered: 无界面自动化；视觉一致性靠人工查看。

## Risks and open questions

- Owner: aisdwf
- Blocker or trigger: None.

## Lessons learned

字符图标的线宽由字体回退决定，太阳和月亮无法靠换字对齐。操作图标必须共用一份描边几何。

## Related documents

- SPECs: [spec-editorial-and-ripple-theme](./spec-editorial-and-ripple-theme[DONE].md)（昼夜按钮与水波纹的既有行为）
- ADRs: None
- Rules: [rule-doc-boundary](../../rules/rule-doc-boundary.md)
- Analysis: [design-visual-language](../../design/design-visual-language.md)
