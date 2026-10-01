# Design: 视觉语言

- **Status**: `active`
- **Owner**: aisdwf
- **Last Updated**: 2026-09-26

> **文档类型**：design —— 只描述**风格约束与视觉原则**，不含实施进度。
> 依 [rule-doc-boundary](../rules/rule-doc-boundary.md)，本文严禁出现
> 复选框、分阶段计划、验证记录。「做到哪了」一律查 `specs/`。
>
> **权威性**：本文是视觉决策的单一真源。所有 UI 实现必须复用此处定义的令牌，
> 严禁另造并行的颜色/尺寸词汇 —— 该问题曾导致一整轮视觉实现全面失效
> （详见 [spec-editorial-and-ripple-theme](../specs/visual-theme/spec-editorial-and-ripple-theme[DONE].md) 的根因分析）。

---

## 1. 设计基调

FlowTask 的视觉语言以「**Editorial（杂志排版）**」为基调，
借鉴现代高级播客/杂志的排版美学。核心特征：

### 1.1 Typography as Interface（文字即界面）

- 焦点是**大字号、强节奏感的标题排版**，而非密集的小格子与表格线；
- 弱化表单分割线，利用**间距与明暗对比**形成天然视线流；
- 副标签采用精致的小字号（Micro-label），悬于标题上方或左侧，主副分明。

### 1.2 极简深邃与高级灰度控制

- 背景**不使用纯黑**，而是沉稳内敛的高级深炭灰；
- 辅助文字采用低纯度但高识别度的中灰；
- 重要标题使用近纯白高光。

### 1.3 呼吸感强烈的条目流

- 每个条目拥有充裕的上下留白（垂直内边距 18–24px）；
- 靠留白与发丝线建立节奏，而非卡片描边。

---

## 2. 色彩令牌

> **实现位置**：`src/FlowTask.Desktop/Styles/Tokens.Light.axaml` 与 `Tokens.Dark.axaml`。
> 深浅主题各持一份色值，以保证两种底色下的对比度均充足。

### 2.1 表面层级

| 用途 | 深色主题参考值 |
| :--- | :--- |
| 窗口底色 | `#141416` |
| 卡片 / 侧边栏 | `#1B1B1E` |
| 悬浮卡片 | `#242429` |
| 随手记浮窗 | `AcrylicBlur` 半透明毛玻璃 `#1E1E24EE` + 边框 `#383842` |

### 2.2 强调色与优先级

| 用途 | 色值 | 柔和微底 |
| :--- | :--- | :--- |
| 品牌主色（聚焦、按钮强调） | 灵动天青蓝 `#3B82F6` | — |
| 优先级 High | 珊瑚红 `#EF4444` | `#3B1C1C` |
| 优先级 Medium | 暖琥珀 `#F59E0B` | `#332514` |
| 优先级 Low | 鼠尾草绿 `#10B981` | `#162C24` |

### 2.3 文字层级

| 层级 | 色值 |
| :--- | :--- |
| 主标题 / 主要信息 | `#F3F4F6` |
| 次级辅助信息 | `#9CA3AF` |
| 快捷键 Badge / 弱标签 | `#6B7280`（背景 `#2B2D35`） |

### 2.4 令牌键名索引

实现中的权威键名（引用时必须完全匹配，Avalonia 对未定义的
`DynamicResource` **静默跳过、不报错**）：

**颜色**：`WindowSurfaceBrush` `SidebarSurfaceBrush` `CardSurfaceBrush`
`CardSurfaceHoverBrush` `FloatingSurfaceBrush` `HairlineBrush` `HairlineStrongBrush`
`TextPrimaryBrush` `TextSecondaryBrush` `TextTertiaryBrush` `TextDisabledBrush`
`AccentBrush` `AccentSubtleBrush` `AccentGlowBrush` `OnAccentBrush`
`PriorityHighBrush` `PriorityMediumBrush` `PriorityLowBrush`（各含对应 `*SurfaceBrush`）
`KeyCapSurfaceBrush` `KeyCapBorderBrush`

危险 / 错误文字用 `PriorityHighBrush`，弹层底色用 `CardSurfaceBrush`，不另设别名。
`DecorationColorTests.EveryDynamicColorKeyInXaml_IsDefined` 扫描全部 axaml，引用未定义的颜色键即失败。

**形状与尺寸**（`Tokens.Shared.axaml`）：`ControlCornerRadius` `CardCornerRadius`
`WindowCornerRadius` `FloatingCapsuleCornerRadius` `PillCornerRadius`
`FontSizeMicro` `FontSizeCaption` `FontSizeBody` `FontSizeTaskTitle`
`FontSizeSectionTitle` `FontSizeHeroTitle` `MicroLabelLetterSpacing`
`HeroTitleLetterSpacing` `TaskRowPadding` `ContentAreaMargin` `SidebarPadding`

---

## 3. 形状与间距

| 元素 | 圆角 |
| :--- | :--- |
| 无边框亚克力窗口 | `14px` |
| 卡片 | `10px` |
| 按钮 / 徽标 | `6px` |

---

## 4. 窗口材质

首发以 Windows 11 Fluent 2 设计风格为基准（Mica / Acrylic 材质），
同时保障 macOS 平台的原生毛玻璃效果。

**关键约束**：侧边栏等面板**不得使用不透明底色** ——
在透明材质档位下，不透明面板会遮盖平台合成的模糊效果，
使材质切换在视觉上毫无差别。应改用极弱的叠加色（如 `HairlineBrush`）。

---

## 5. 动效

### 5.1 昼夜切换水波纹

右上角单一昼夜切换圆纽（深色显示太阳、浅色显示月亮，与其它操作图标同一描边），点击时颜色变化以**径向圆环**
从按钮坐标点向整个屏幕平滑扩散，达成无闪烁换肤。

- 扩散时长 ≈ 520ms（兼顾丝滑观感与不拖慢操作节奏）
- 遮罩淡出 ≈ 260ms

**已知框架边界**（实验确认，非推断）：Avalonia 11.2 的 `Animation`
支持 `Opacity` 但**不支持 `RenderTransform`**；
官方文档提示的 `Animation.Animators` 在 11.2 已移除。

### 5.2 微交互

- 任务勾选完成：优雅的透明度渐变与删除线沉降
- 行内操作按钮：静默隐藏，悬停整行才浮现，避免视觉噪音
- 快速添加栏：按回车「如呼吸般自然落入列表」

### 5.3 浮层与展开不突变

用户原话（2026-09-29）：「日历展开感觉违背了 ui 不能突变的原则，突然展开了一块。」

- 临时内容（选择器等）以浮层叠加，**不推动**既有布局；
- 出现：透明度 0→1 + 约 6px 下移归位，≈160ms，CubicEaseOut；消失可直接关闭；
- 受 §5.1 框架边界约束，位移用 `Transitions` 而非 `Animation` 实现。

---

## 6. 双窗口的视觉分工

| 视窗 | 视觉定位 |
| :--- | :--- |
| **主工作台** | 一体化沉浸视窗，Mica 材质全屏铺底，无多余边框；左侧极简导航 + 右侧 Hero 级任务流 |
| **随手记浮窗** | Raycast / Spotlight 风格的悬浮胶囊，毛玻璃质感，无系统装饰，无多余干扰元素 |

> **注**：两个窗口的**功能**分工见
> [design-interaction-principles](./design-interaction-principles.md)，
> 本文只约束视觉表现。

---

## 7. 操作图标

纯操作图标共用一套 24×24 描边几何（`Styles/Icons.axaml`），圆头、圆角连接。光学尺寸由按钮决定，不另造填充版或彩色变体。

线宽分两档（spec-icon-refresh，所有者原话「对于这样的几个按钮来说，我觉得线条有点太细了」「垃圾桶这些效果还不错」）：

| 档位 | 描边（24 基准） | 显示尺寸 | 屏幕线宽 | 用于 |
| :--- | :--- | :--- | :--- | :--- |
| 常规 `Path.StrokeIcon` | 1.75 | 13–16px | 约 1.0px | 行内与弱操作 |
| 加粗 `Path.StrokeIcon.Bold` | 2.25 | 20px | 约 1.9px | 右上角 40px 圆钮 |

圆形一律写成两段半圆并闭合，不用「终点差 0.01」的近似闭合：圆头端点会在接缝处叠出鼓包。

承载描边图标的 `Viewbox` 不得裁剪（`ClipToBounds=False`，顶栏用 `Viewbox.HeaderIcon`）：`Stretch=Uniform` 只把几何外框拉满，描边会向外溢出半个线宽，默认裁剪会把外沿切平。

| 用途 | 几何 | 颜色 |
| :--- | :--- | :--- |
| 昼夜切换 | 太阳 / 月亮 | 次级文字 |
| 设置、指南 | 6 齿齿轮、圆内问号 | 次级文字 |
| 返回 | 左箭头 | 次级文字 |
| 删除、新建 | 垃圾桶、关闭、加号 | 三级文字 |
| 置顶 | 图钉 | 未置顶三级文字，已置顶强调色 |
| 归档、恢复 | 归档盒、归档盒 + 上箭头 | 三级文字 |
| 快捷小窗输入标记 | 星标 | 强调色 |

## 7.1 修饰色

修饰色全部从当前主题派生，不存进用户数据，也没有单独的配色设置（spec-theme-bound-decoration-colors，owner 裁决「绑定主题」「统一单色」）。

| 修饰 | 颜色 |
| :--- | :--- |
| 项目色点（侧栏、任务元信息行、小窗项目下拉） | `AccentBrush`，所有项目同色 |
| 任务行色条 P1 | `PriorityHighBrush`（语义红） |
| 任务行色条 P2 | `AccentBrush` |
| 任务行色条 P3 | `AccentGlowBrush`（强调色半透明） |
| P1 / P2 / P3 微标签 | 保持语义色 `PriorityHigh/Medium/Low*` |
| Fluent 原生控件（单选、复选、下拉、日历） | `SystemAccentColor*`，换主题时由 `AppearanceCoordinator` 写入主题强调色 |

色条表达优先级强度，不再表达项目归属。曾经的项目色（`Project.ColorHex`）以字面 hex 存库、主题够不到，换主题后仍是固定紫 / 绿，已删除。

## 8. 历史沿革

本文合并自两份早期设计文档：

- **现代视觉风格与双窗口交互重塑**（原 design-visual-language）—— 贡献了色彩、圆角、间距规范；
  其「功能边界拆分」章节已被实现取代，移入
  [`archived/`](../archived/design-visual-ux-overhaul-superseded.md)
- **大开大合排版与水波纹昼夜切换**（原 design-visual-language）—— 贡献了 Editorial 基调与动效意图

两份原文档的设计来源均为 `res/` 下的单张视觉参考图。
