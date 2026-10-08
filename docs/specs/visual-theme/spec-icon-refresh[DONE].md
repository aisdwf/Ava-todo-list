# spec-icon-refresh: 顶栏图标重绘加粗，新增置顶 / 归档 / 恢复几何

## Metadata

- **ID**: spec-icon-refresh
- **Type**: simple
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-10-01
- **Last Updated**: 2026-10-01

## Why

所有者原话（2026-10-01）：「齿轮不协调，太阳月亮有种画笔断触的感觉。对于这样的几个按钮来说，我觉得线条有点太细了，可以优化一下风格。垃圾桶这些效果还不错」。

现状核对（`Styles/Icons.axaml`、`EditorialStyles.axaml:147`）：

- 顶栏图标为 1.75 描边 × Viewbox 18/24，屏幕上约 1.31px，比行内垃圾桶的视觉重量还轻（行内图标虽更细，但尺寸小、颜色淡，不显单薄）。
- `IconSun` / `IconHelp` 的圆写作 `A5,5 0 1 1 11.99,7`：起止点差 0.01、路径不闭合，圆头端点在接缝处叠出一个小鼓包，这就是「断触」感的来源。太阳 8 根光芒长 2、离圆 3，散而不连。
- `IconSettings` 是 Feather 式 8 叶花瓣齿轮，几十段小弧，缩到 18px 后轮廓发糊，与其余几何（直线 + 大弧）语言不一致。

另：后续 `project-archive` / `task-pin` 分支需要置顶、归档、恢复图标。所有者要求「置顶设计新的svg」并与本轮一起出图，故几何在此一次定稿。

## What

所有者在候选页中选定 **「A 加粗」**（2026-10-01）。

1. `Icons.axaml` 替换 4 个几何：

   | Key | 新几何（24×24） |
   | --- | --- |
   | `IconSettings` | 6 齿齿轮：齿顶直线、齿间沿 r=7 根圆弧，单条闭合路径 + r=3 闭合轴孔 |
   | `IconSun` | r=4 闭合圆 + 8 根短光芒（r 7→9.5） |
   | `IconMoon` | 单条闭合月牙（外弧 r=9，内弧 r=6.5） |
   | `IconHelp` | 圆改为两段半圆闭合（问号笔画不变） |

2. 新增 3 个几何（本分支不接入界面，由后续分支引用）：`IconPin`（图钉）、`IconArchive`（归档盒）、`IconRestore`（归档盒 + 上箭头）。
3. 新增样式类 `Path.StrokeIcon.Bold`（`StrokeThickness=2.25`），只用于顶栏 `IconCircle` 三钮；三钮 Viewbox 18 → 20。屏幕线宽约 1.88px。
4. 操作指南「切换昼夜」场景（`GuideScenes.cs:864-870`）的三个顶栏图标同步用新几何与加粗线宽，避免指南里还是旧图标。
5. `design-visual-language.md` §7 更新线宽约定：行内 1.75，顶栏 2.25。

## Non-goals

- 行内图标（垃圾桶、X、加号、日历、星标）不改几何、不改线宽：所有者原话「垃圾桶这些效果还不错」。
- 不改按钮尺寸（40×40）、位置、悬停与按压动效。
- 不改品牌图标与托盘图标。

## Constraints and decisions

- Article 6：几何只在 `Icons.axaml` 一处；线宽差异用样式类表达，不在各按钮上写死 `StrokeThickness`。
- 候选页按 Avalonia `Stretch=Uniform` + `Viewbox` 的缩放方式模拟；浏览器与 Skia 抗锯齿有差异，以预览 exe 为准。
- **[推断]** 指南场景的 `SceneKit.Icon` 直接以像素绘制（无 Viewbox），加粗档按 2.25 × 18/24 ≈ 1.7 给定。依据：与主窗顶栏屏幕线宽保持同一比例。待预览确认。

## Acceptance criteria

- [ ] 顶栏「? / 设置 / 昼夜」用新几何，浅色与深色主题下线宽一致、无接缝鼓包。
- [ ] 昼夜切换的显隐与水波纹动效不变。
- [ ] 指南「切换昼夜」场景显示新图标。
- [ ] 行内图标外观不变。
- [x] `dotnet build` 0 警告 0 错误；`dotnet test` 不低于 341（2026-10-01：修复切边后 342 通过）。
- [x] 顶栏图标外沿不被裁切（所有者复验 2026-10-02「没问题」）。
- [x] 所有者预览确认（2026-10-02）。

## Staged plan

1. `Icons.axaml` 替换 4 个几何、新增 3 个几何。
2. `EditorialStyles.axaml` 增 `Path.StrokeIcon.Bold`；`MainWindow.axaml` 顶栏三钮换类、Viewbox 改 20。
3. `SceneKit.Icon` 增可选线宽参数；`GuideScenes` 顶栏三图标传入加粗线宽。
4. 更新 `design-visual-language.md` §7；build + test；发布预览。

## Change checklist

- [x] `src/FlowTask.Desktop/Styles/Icons.axaml`
- [x] `src/FlowTask.Desktop/Styles/EditorialStyles.axaml`
- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml`（顶栏三钮）
- [x] `src/FlowTask.Desktop/Views/Guide/SceneKit.cs`
- [x] `src/FlowTask.Desktop/Views/Guide/GuideScenes.cs`（昼夜场景）
- [x] `docs/design/design-visual-language.md` §7
- [x] `tests/FlowTask.Tests/HeaderIconClipTests.cs`（顶栏 Viewbox 不裁剪描边）
- [x] Guide: not affected — 仅图标重绘，位置、手势与文案不变；场景图标同步换新几何，tour 版本不升

## Progress log

### 2026-10-01

- Completed: 候选页（现行 / A / B × 常规 / 加粗，浅深两色）交所有者挑选；所有者选「A 加粗」。
- Decisions: 只加粗顶栏；行内保持 1.75。置顶 / 归档 / 恢复几何随本分支定稿。
- 所有者确认「先执行icon」，SPEC 改 `[IN-PROGRESS]`。
- Completed: 计划 1–4 全部落地。`Icons.axaml` 换 4 个几何并新增 `IconPin` / `IconArchive` / `IconRestore`；新增 `Path.StrokeIcon.Bold`（2.25）；顶栏三钮 Viewbox 18 → 20 并用加粗档；`SceneLayer.Icon` 增线宽参数，昼夜场景顶栏用 `HeaderIconStroke`（1.7）；`design-visual-language` §7 改为两档线宽表。
- Automated: build 0 警告 0 错误；test 341 通过。
- Preview: `preview/feature/icon-refresh/FlowTask.exe`。
- 所有者预览反馈：「看起来四周有一点像是被切割一样的痕迹」。
- 根因（code wrong）：`Path.Stretch=Uniform` 只按几何**填充外框**缩放到 24×24，描边仍向外溢出半个线宽（2.25 时 1.125）；`Viewbox` 默认 `ClipToBounds=True`，把齿顶、光芒末端、圆最外沿切平。headless 实测：填充框 `0,0,24,24`，描边框 `-1.125,-1.125,26.25,26.25`。旧 1.75 线宽同样被切，但细线下不显眼，加粗后暴露。
- 修复：新增 `Viewbox.HeaderIcon` 样式（20×20、`ClipToBounds=False`），顶栏四个 Viewbox 改用该类。40px 圆钮内余量充足，溢出约 0.9px 不触边。指南场景的 `SceneLayer.Icon` 不经 Viewbox，不受影响。
- 测试：新增 `HeaderIconClipTests`；已验证 `ClipToBounds=True` 时该测试失败、修复后通过。全量 342 通过，build 0/0。
- 所有者复验（2026-10-02）：「没问题，该项合入dev并处理文档」。
- Current resume point: 关闭 `[DONE]`，提交后经 `finish-task.ps1` 合入 `dev`。

## Verification

- Automated: `dotnet build`、`dotnet test`（`GuideSceneRenderTests` 覆盖场景构建；`HeaderIconClipTests` 守护顶栏不裁切）。
- Manual: 所有者用 `preview/feature/icon-refresh/FlowTask.exe` 查看浅色 / 深色顶栏与指南场景（首轮反馈切边，修复后重新发布复验通过，2026-10-02）。
- Not run or not covered: 无像素级截图比对。

## Risks and open questions

- Owner: aisdwf
- 新增的 3 个几何在本分支无引用方。若后续分支方案变化，须回到此处删改，不得留死资源。

## Lessons learned

- 描边图标的可见外沿 = 几何外框 + 半个线宽。`Stretch=Uniform` 把几何外框拉满容器，描边必然溢出；任何默认裁剪的容器（`Viewbox`）都会把它切平。加粗线宽前要先确认容器不裁剪。
- 浏览器候选页用 `getBBox` + 半线宽内边距算 viewBox，天然不切边，所以预览页看不出这个问题；候选页与 Avalonia 的缩放模型并不完全等价。

## Related documents

- SPECs: [spec-unified-svg-icons](./spec-unified-svg-icons[DONE].md)（描边图标体系来源）
- ADRs: None
- Rules: AI_CONSTITUTION Article 6
- Analysis: [design-visual-language](../../design/design-visual-language.md) §7
