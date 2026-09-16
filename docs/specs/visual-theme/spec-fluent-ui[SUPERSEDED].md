# spec-fluent-ui: FlowTask Windows 11 Fluent 2 设计语言与视窗风格重构

> **Redirect**: 本 SPEC 的视觉方案已被
> [spec-editorial-and-ripple-theme](./spec-editorial-and-ripple-theme[DONE].md) 取代，
> **不可作为实现依据**。
>
> 具体差异：本 SPEC 规划的双套 Design Tokens 与「侧边栏底部主题切换分段控件」，
> 在 spec-editorial-and-ripple-theme 中分别改为 `Styles/Tokens.*.axaml` 三文件拆分与「右上角单一圆纽 + 水波纹扩散」。
> 当前 Design Tokens 与主题切换的权威描述见 spec-editorial-and-ripple-theme.1。
>
> **状态订正说明**：本文件名早前已标注 `[DONE]`，但元数据长期停留在 `in-progress`，
> §3 的实施勾选框亦未更新 —— 属于文件名与元数据的状态漂移（违反 Article 1
> 「文档与代码同步维护」）。2026-09-14 收尾时订正为 `superseded` 并补此重定向。
> 未勾选的条目并非未完成，而是其方案已被 spec-editorial-and-ripple-theme 以不同形式实现。

## Metadata

- **ID**: spec-fluent-ui
- **Type**: complex
- **Status**: superseded
- **Owner**: aisdwf
- **Created Date**: 2026-09-14
- **Last Updated**: 2026-09-14
- **Superseded By**: [spec-editorial-and-ripple-theme](./spec-editorial-and-ripple-theme[DONE].md)

---

## 1. Why (Problem & Context)

当前初代 UI 呈现出简陋感，缺乏第一目标平台（Windows 11）特有的质感与现代桌面级视窗标准：
1. **未融入 Windows 11 Fluent 2 设计规范**：缺乏原生级 Mica（云母）/ Acrylic（亚克力）材质融合，窗体未实现原生沉浸式一体化标题栏。
2. **随手记小窗缺乏呼吸感**：窗口形态机械，缺乏类似 Windows 搜索栏或 Raycast 那种居中悬浮、高质感投影、边框 1px 微光（Subtle Highlight）的精致胶囊设计。
3. **主工作台模块扁平单调**：卡片未体现 Fluent 2 Elevation 阴影层级与悬浮态，视觉节奏平淡。

---

## 2. What (Scope & Boundaries)

- **Goals**:
  1. **构建 Windows 11 Fluent 2 视觉基线**：
     - 主视窗与随手记小窗开启 Mica / Acrylic 材质模糊，标题栏一体化融合（`ExtendClientAreaToDecorations="True"`）；
     - 引入 Fluent 2 官方调色阶：深色底色 `#202020` / `#1C1C1C`，卡片表面 `#2B2B2B`，细致 1px 高光边框 `#FFFFFF15`，悬浮微动效；
     - 字体规范：优先选用 `Segoe UI Variable Text`, `Segoe UI`, `Inter`。
  2. **重塑悬浮灵感捕捉小窗（Quick Capture Capsule）**：
     - 尺寸调整为紧凑的药丸卡片（560x170），悬浮居中偏上；
     - 居中放置 Windows 搜索栏风格的精致输入框，右下角展示优雅的键盘指示徽章（`Esc`、`↵ Enter`）。
  3. **重塑主工作台（Main Workspace）**：
     - 沉浸式侧边栏：内置集成式标题区，优雅的 Fluent 风格分类胶囊（含未完成数量圆点徽章）；
     - 现代卡片式待办列表：左侧带优雅的优先级指示彩条与自定义单选框，悬浮柔光反馈。
- **Non-Goals**:
  - 本阶段聚焦于**总体 UI 风格与视觉质感构建**，暂不推进复杂的子清单与多级标签等纵深业务逻辑，确保视觉品质达标。
- **Impacted Files**:
  - `src/FlowTask.Desktop/App.axaml`
  - `src/FlowTask.Desktop/Views/MainWindow.axaml`
  - `src/FlowTask.Desktop/Views/QuickCaptureWindow.axaml`

---

## 3. Phased Implementation Plan

- [x] **Phase 1: Windows 11 Fluent 2 风格系统与 Design Tokens 建立**
  - [x] 在 `App.axaml` 中建立完整的 Fluent 2 Dark 材质字典（Mica 基础色、卡片 Elevation、1px 边框色、Accent Fluent Blue `#0078D4`）；
  - [x] 配置标准 Win11 圆角（Button 4px, Card 8px, Floating 12px）。
- [x] **Phase 2: 极速随手记悬浮胶囊窗重铸**
  - [x] `QuickCaptureWindow.axaml` 配置全透明背景与 Mica / AcrylicBlur；
  - [x] 实现 1px 微光边框 + 40px 深度毛玻璃扩散阴影；
  - [x] 精致化快捷键提示徽章与输入交互。
- [x] **Phase 3: 主工作台沉浸式视窗重塑**
  - [x] `MainWindow.axaml` 开启一体化标题栏沉浸渲染；
  - [x] 重构左侧导航侧栏与任务卡片流布局；
  - [x] 优化待办卡片微交互与优先级色彩映射。
- [x] **Phase 4: 编译、门禁运行与视窗启动验证**
  - [x] 执行单元测试与构建门禁；
  - [x] 启动程序验证视觉效果与流畅度。
- [ ] **Phase 5: Windows 11 Light/Dark 主题双模态体系与实时切换**
  - [ ] 在 `App.axaml` 中定义 `<ResourceDictionary.ThemeDictionaries>`，建立完整的 Windows 11 Fluent 2 浅色（Light）与深色（Dark）双套 Design Tokens；
  - [ ] 在侧边栏底部添加 Fluent 2 风格的主题切换分段控制按钮（☀️ 浅色 / 🌙 深色）；
  - [ ] 在 `MainViewModel` 中绑定主题切换命令，实现 `Application.Current.RequestedThemeVariant` 毫秒级热切换；
  - [ ] 主窗口与随手记小窗全部颜色刷子改用 `{DynamicResource}` 引用，实现双视窗瞬时无感换肤。

---

## 4. Verification Plan

- **自动化验证**:
  - `dotnet test FlowTask.sln` 保证仓储与领域逻辑完全正常。
  - `dotnet build FlowTask.sln` 保证 0 Warning 0 Error。
- **视觉交互验收**:
  - 启动应用验证主视窗是否具备一体化沉浸边框与 Fluent 2 质感；
  - 验证随手记小窗呼出是否有明显的悬浮胶囊美感与键盘流支持。

---

## 5. Commit Attribution

- **why**: 修正首版 UI 过于简陋平淡的问题，对标 Windows 11 Fluent 2 设计语言提升桌面级原生质感；
- **what**: 重写 App.axaml、MainWindow.axaml、QuickCaptureWindow.axaml，注入 Mica/Acrylic 与 Fluent 2 Design Tokens。
