# spec-settings-master-detail-and-theme-presets: 设置页改为主从式独立页面 + 可扩展命名主题预设

## Metadata

- **ID**: spec-settings-master-detail-and-theme-presets
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-23
- **Last Updated**: 2026-09-26

---

## Why

当前"设置"不是弹窗，而是把主窗口右侧整个内容区替换成一根竖直滚动的卡片流
（`MainWindow.axaml:582-785`，`Border.SettingsCard` 依次排开：窗口材质 / 强调色 /
默认到期偏移 / 标签管理 / 关于）。用户反馈这种单页面卷动式管理所有设置内容的方式
不够清晰，希望改为"左侧具体设置项列表、右侧对应内容"的主从式（master-detail）
独立页面，页面左上角提供返回按钮——这是目前很多桌面级系统偏好设置面板
（以及 newapi 类站点的设置面板）采用的结构。

同时，当前"主题"能力仅是 4 个孤立的强调色（科技蓝/极光紫/翡翠绿/日落橙，
`AppearanceCoordinator.cs:37-43`）叠加深浅两种 `ThemeVariant`，并不存在
"Anthropic""暗夜""海风"这类**命名主题预设**——用户希望引入这类预设，
参照 dogapi.cc / linkapi.ai 两个站点右上角"调色盘"入口里的风格设置面板做法，
但不需要超大字号，且要保留现有的强调色/材质概念，只是把"设置组织方式"与
"主题预设的丰富度"一起做一次结构性升级。

### Root cause of the current limitation

不是缺陷，是当前实现范围本就只覆盖"强调色 + 材质 + 昼夜"三个独立轴，
`spec-editorial-and-ripple-theme[DONE]` 交付时也只承诺了这三样。
本 SPEC 是在其基础上做外观设置体系的下一轮扩展，不是修 bug。

---

## What

### 范围内

1. **设置页导航结构重做**：把"点击齿轮 → 整个内容区替换为一根卷动卡片流"
   改为"点击齿轮 → 进入设置页 → 左侧窄栏是设置项列表（外观主题 / 强调色 /
   窗口材质 / 默认到期偏移 / 标签管理 / 关于），右侧宽栏渲染当前选中设置项的内容；
   页面左上角有一个「← 返回」按钮回到任务清单"。这是主窗口内嵌的两级视图切换
   （不新开 `Window`），复用现有 `IsSettingsOpen` 门控内容区切换的机制，
   在其内部新增一层"当前选中设置项"的二级状态。
2. **标签管理**从"设置卷动流里的一张卡片"变成"左侧设置项列表中的独立一项"，
   行为（新建/改名/删色/删除）不变，只改变承载位置。
3. **窗口材质**保持为独立设置项，不与主题预设合并（用户已确认）。
4. **引入"主题预设"（Theme Preset）概念**，作为强调色选择器的替代/上位：
   - 数据结构上从"仅一个强调色种子色"扩展为"一个具备名称、强调色、
     （可选）表面基调覆写的预设对象"，为承载 newapi 类站点里那种
     "选一个主题=同时决定多个视觉变量"的做法打好架构基础。
   - **具体预设的数量与色值本 SPEC 不预先定义**（见下方 Non-goals 与
     Risks/open questions）——除已有的 4 个强调色可作为迁移基线外，
     "Anthropic""暗夜""海风"等命名预设需要先采集 dogapi.cc / linkapi.ai
     的真实调色盘面板内容，本 SPEC 只交付可承载任意数量命名预设的架构，
     不虚构色值。
5. 保留：昼夜（Dark/Light）水波纹切换、材质切换动效、SQLite 设置持久化机制、
   现有 `AppearanceCoordinator` 的令牌写入方式（`WriteThemeResource` 等）。

### 范围外（见 Non-goals）

- 具体新主题预设的最终名称/色值定义。
- 独立 Settings `Window`（用户已选择"主窗口内嵌整页"）。
- 标签管理搬出设置体系。
- 材质与主题预设合并。
- newapi/dogapi/linkapi 的像素级复刻——只做"设置组织方式 + 多主题预设"这一
  结构性参照，不做视觉抄版（且这两个站点面板本身未被访问到，见 Risks）。

### 影响模块

- `src/FlowTask.Desktop/Views/MainWindow.axaml`：设置区 XAML 从单栏 `ScrollViewer`
  改为 `Grid`（左侧设置项列表 + 右侧内容宿主 + 顶部返回按钮）。
- `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`：新增"当前选中设置项"状态
  与切换命令；`IsSettingsOpen` 语义不变（仍是"是否在设置页"），新增内部导航状态。
- `src/FlowTask.Desktop/Appearance/AppearanceCoordinator.cs`：`AccentPresets` 的
  数据形态评估是否升级为 `ThemePreset`（含名称 + 强调色 + 可选表面覆写），
  同时保持向后兼容（现有 4 色必须能无损迁移，避免用户已保存的
  `SelectedAccent.Id` 失效）。
- `src/FlowTask.Desktop/Styles/EditorialStyles.axaml`：新增左侧设置项列表的
  样式类（预计命名 `SettingsNavList` / `SettingsNavItem`，最终命名在实施阶段
  与既有 `ChoiceGrid` 系列风格对齐后确定）。
- 不涉及 `FlowTask.Core` / `FlowTask.Infrastructure`（本次不新增持久化字段，
  当前设置持久化只有 `default_due_offset_days` 一项，主题/材质/强调色选择
  目前是否持久化需要在 Constraints 中确认——见下方决策项）。

---

## Non-goals

- 不在本 SPEC 内定案 Anthropic/暗夜/海风等预设的具体色值——色值采集是
  一个独立的、依赖浏览器操作能力的调研任务（见 Risks），色值确定后如需要，
  可在本 SPEC 的后续阶段或一个新的小 SPEC 中补齐，不阻塞本次导航结构重做。
- 不引入用户自定义主题编辑器（自选任意色值拼主题）——本次只做"选预设"，
  不做"造预设"。
- 不改动任务清单主视图（`!IsSettingsOpen` 分支）的任何交互或样式。
- 不新开独立 Settings `Window`。
- 不改动标签管理的业务行为，只改动其在设置页中的承载位置。

---

## Constraints and decisions

- **AI_CONSTITUTION Article 6**（单一真源）：主题预设的权威定义继续集中在
  `AppearanceCoordinator`，View/ViewModel 不得另建色值查找表——延续现状约束。
- **AI_CONSTITUTION Article 9**（禁止用 sleep/进程存活冒充验证）：本次改动
  含动效相关代码（材质/主题切换事件），验证必须描述实际观察到的视觉结果，
  不得以"编译通过"代替。
- **rule-no-invented-user-behavior**：本 SPEC 中任何未直接来自用户原话的
  交互细节决策，必须标注 `[推断]` 并给出可检验依据；无法判断的留在
  "Risks and open questions" 提问，不擅自填入倾向性方案。
- **用户已确认的决策**（原话摘要，均来自本次前置提问的回答）：
  1. 设置页导航结构 = "两级：主窗口内嵌整页（推荐）"。
  2. 标签管理 = "作为设置页左侧的一个独立设置项（推荐）"。
  3. 材质 = "保留材质为独立设置项（推荐，风险低）"。
  4. Anthropic/暗夜/海风的具体色值 = "先记录待办，后续用 browser-use agent
     采集配色（推荐）"——用户明确说明 dogapi.cc / linkapi.ai 右上角有
     "类似调色盘的按钮"，点进去是这两个站点自己的风格设置面板，需要先看
     面板里实际的预设与配色，而非由本次执行者猜测定义。
- **[推断] 左侧设置项分组顺序**：外观主题 → 强调色 → 窗口材质 →
  默认到期偏移 → 标签管理 → 关于。
  **推断依据**：延续当前卷动流的既有顺序（`MainWindow.axaml:588-783`
  实际排列），只是把"顺序"从"卷动位置"改为"列表位置"，不引入新的信息架构
  判断。若用户认为分组或顺序需要调整，需在 Step 2 计划确认时提出。
- **待决策（非推断，需用户明确回答，见 Risks 第 1 条）**：主题预设/强调色/
  材质的选择是否需要持久化到 SQLite（目前 `IAppSettingsRepository` 只有
  `default_due_offset_days`，重启应用后强调色/材质选择会回退到默认值——
  需先确认这是已知的现状缺口还是本次要顺带修的范围）。

---

## Acceptance criteria

- [x] 点击右上角齿轮进入设置页后，设置占满整个窗口。窗口左侧栏变成设置选项
  （外观主题、强调色、窗口材质、默认到期偏移、关于），右侧只显示当前选中项的内容。
  任务导航在设置页内不可见，返回后恢复。所有者看过预览后要求合入 `dev`。
- [x] 返回入口在左侧选项栏最顶部，点击后回到任务清单。右侧页头与「关于」里
  不再有第二处返回。
- [x] 左侧列表任一项被选中时，右侧内容区渲染且仅渲染该项对应的设置内容。
- [x] 标签管理已由 `spec-remove-tag-feature` 从设置页移除，本条不再适用。
- [x] 窗口材质选择器保持独立，选中后即时应用材质背景。
- [x] `ThemePresets` 是权威列表，当前为 9 套参考站点色板，XAML 不依赖固定数量。
- [x] Anthropic/暗夜/海风等预设色值已从 dogapi.cc 与 linkapi.ai 的样式表采集
  （同一构建 `2k6e8r7p`）。Anthropic 切换衬线字体；非默认主题的侧栏用
  `SidebarWashBrush` 朝强调色轻偏。「超大字体简易」未收入。
- [x] 外观主题卡片铺满右侧内容区（三列、色带高度 88），不再挤在 640px 两列扁条里。
- [x] `dotnet test FlowTask.sln -c Debug` → 183 通过 / 0 失败（2026-09-26，合入 `dev` 后）。
- [x] 字体尺寸未整体放大——`Tokens.Shared.axaml` 只新增可覆写的 `AppFontFamily`，
  未引入更大字号。

---

## Staged plan

> 呈现给用户审核，收到显式确认后才可将本 SPEC 状态改为 `in-progress` 并开工
> （rule-spec-review-gate）。

1. **信息架构确认**：把上方"左侧设置项分组顺序"的 `[推断]` 提交用户复核，
   同时确认"主题/材质选择是否需要持久化"这一待决策项。
2. **XAML 结构重做**：把 `MainWindow.axaml:582-785` 的单栏 `ScrollViewer`
   改为 `Grid`（左侧 `ListBox` 承载设置项、右侧内容宿主随选中项切换、
   顶部返回按钮），标签管理与关于两块内容原样迁入新结构，不改动其内部绑定。
3. **ViewModel 状态扩展**：在 `MainViewModel` 中新增"当前选中设置项"的
   状态与切换逻辑（不新增额外 Window 或 Service），保持 `IsSettingsOpen`
   现有语义与调用点不变。
4. **主题预设架构准备**：评估并重构 `AppearanceCoordinator.AccentPresets`
   为可承载"名称 + 强调色 +（可选）表面基调覆写"的结构，现有 4 色原样迁移，
   不引入新色值；确认迁移不破坏 `SelectedAccent`/`SelectedMaterial` 现有绑定。
5. **样式补齐**：在 `EditorialStyles.axaml` 中新增左侧设置项列表样式，
   与现有 `NavPill`/`ChoiceGrid` 风格语言保持一致，不引入新字号尺度。
6. **验证**：`dotnet build`、`dotnet test`（记录基线通过数与本轮结果对比）、
   手动核对设置页各项切换与标签管理行为；更新本 SPEC 的 Progress log。
7. **待办移交**：把"Anthropic/暗夜/海风色值采集"整理成明确的后续任务，
   记录在本 SPEC 的 Risks/open questions 中，供后续 browser-use agent 接手，
   不在本 SPEC 内假装已完成。

---

## Change checklist

- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml`：设置区结构重做
  （左侧设置项列表 + 右侧内容 + 返回按钮）。页头区新增返回按钮分支，
  右上角齿轮在设置页内隐藏。
- [x] `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`：新增
  `SettingsSection`/`SelectedSettingsSection`/`SelectedSettingsNavItem`/
  `SettingsNavItems`/`SelectSettingsSectionCommand`；`ToggleSettings` 打开时
  重置到首项。
- [x] `src/FlowTask.Desktop/ViewModels/SettingsSection.cs`（新文件）：
  `SettingsSection` 枚举 + `SettingsNavItem` 记录类型。
- [x] `src/FlowTask.Desktop/Appearance/AppearanceCoordinator.cs`：新增
  `ThemePreset`/`ThemeSurfaceOverride` 记录类型、`ThemePresets`
  （回填现有 4 色）、`FindThemePreset`/`ApplyThemePreset`；`AccentPresets`
  与 `AppearanceOption` 原样保留，未破坏既有绑定。
- [x] `src/FlowTask.Desktop/Styles/EditorialStyles.axaml`：新增
  `ListBox.SettingsNav` 及其 `ListBoxItem` 系列样式（视觉呼应 `NavPill`）。
- [x] `docs/specs/README.md`：新增本 SPEC 索引行、待办事项索引新增一行、
  更新「新会话接手入口」当前阶段描述。
- [x] 命名主题色板写入 `ThemePresets`；Anthropic 衬线字体；非默认侧栏轻染色。
- [x] 外观主题选择改为铺满右侧的三列色板卡片。
- [x] 所有者预览通过并合入 `dev` 后，本 SPEC 转 `[DONE]`。

---

## Progress log

### 2026-09-23

- Completed: 完整阅读现有设置 UI（`MainWindow.axaml`）、`AppearanceCoordinator`、
  `MainViewModel` 相关属性/命令、令牌文件（`Tokens.Dark/Light/Shared.axaml`）、
  `AppSetting`/`IAppSettingsRepository`/`SqliteAppSettingsRepository`；确认
  当前无 Anthropic/暗夜/海风预设、也无独立主题预设概念，仅有 4 个强调色。
- Decisions: 记录见上方 Constraints and decisions 中"用户已确认的决策"。
- Current resume point: SPEC 处于 `draft`，尚未开工任何代码改动。
  下一步是把本 SPEC 与 Staged plan 呈现给用户，等待显式确认后方可转
  `in-progress` 并开始 Step 2。
- Subagent/task references: 无（本轮探索由主会话直接完成，未派发子代理）。
- 尝试访问 `dogapi.cc/dashboard/overview`：返回 451（不可达）；
  `linkapi.ai` 需登录未访问；GitHub 对 `QuantumNous/new-api` 前端主题源码的
  代码搜索需登录，未能读取其真实预设定义——因此本 SPEC 未采用任何来自这两个
  参考站点的具体配色数据，全部相关空白已记录在 Risks and open questions。

### 2026-09-23（用户确认，转 in-progress）

- Completed: 用户回复"确认，请开始"，对本 SPEC 呈现的 7 步 Staged plan 整体确认。
- Decisions:
  1. **左侧设置项分组顺序**：采用 Constraints 中 `[推断]` 的顺序
     （外观主题 → 强调色 → 窗口材质 → 默认到期偏移 → 标签管理 → 关于），
     用户确认回复未提出异议，视为对该推断的接受。
  2. **主题/强调色/材质选择是否持久化**：用户确认回复未显式回答该问题。
     依据 scope discipline（用户要求 A 只做 A，不擅自扩大范围），本轮**保持现状
     不做持久化**——即本次改动只重排设置页的组织结构与主题预设架构，
     不新增任何 SQLite 写入路径。若用户实际期望一并修复持久化缺口，
     需另行提出，届时作为独立变更处理，不追溯合并进本轮 acceptance criteria。
- Current resume point: SPEC 转为 `in-progress`，开始执行 Step 2（XAML 结构重做）。
- Subagent/task references: 无。

### 2026-09-23（实施完成，待用户手动验证）

- Completed:
  1. `AppearanceCoordinator.cs`：新增 `ThemePreset`/`ThemeSurfaceOverride`
     记录类型、`ThemePresets`（由 `AccentPresets` 派生回填）、
     `FindThemePreset`/`ApplyThemePreset`；`AccentPresets`/`AppearanceOption`
     原样保留未改动。
  2. `SettingsSection.cs`（新文件）：`SettingsSection` 枚举
     （ThemePreset/Accent/Material/DueDateOffset/Tags/About）+
     `SettingsNavItem` 记录类型。
  3. `MainViewModel.cs`：新增 `SelectedSettingsSection`（权威状态）、
     `SelectedSettingsNavItem`（ListBox.SelectedItem 桥接属性）、
     `ThemePresets`/`SelectedThemePreset`、`SettingsNavItems`（固定 6 项列表）、
     `SelectSettingsSectionCommand`；`ToggleSettings` 打开设置页时重置到
     首项 `ThemePreset`。
  4. `MainWindow.axaml`：
     - 页头 `IsSettingsOpen` 分支改为「← 返回」按钮 + "外观偏好"标题；
     - 右上角齿轮按钮加 `IsVisible="{Binding !IsSettingsOpen}"`；
     - 设置区从单栏 `ScrollViewer` 改为 `Grid`（左 216px 导航 `ListBox` +
       右侧内容 `ScrollViewer`），六个设置分区各自用
       `EnumChoiceConverter` 比对 `SelectedSettingsSection` 控制显隐；
     - 标签管理、关于两块内容原样迁入，内部绑定未改动。
  5. `EditorialStyles.axaml`：新增 `ListBox.SettingsNav` 系列样式
     （视觉呼应既有 `NavPill`：左侧选中竖条 + 淡色底，未引入新字号）。
  6. `docs/specs/README.md`：新增本 SPEC 索引行、待办事项索引新增
     "Anthropic/暗夜/海风预设色值未定义"一行、更新接手入口当前阶段描述。
- Decisions:
  - **[推断]** 设置页内隐藏右上角齿轮按钮（返回入口已由左上角「←」承担，
    避免同一屏出现两个"离开设置页"的入口）。推断依据：齿轮按钮原本的唯一
    职责就是进入设置页，进入后再展示它没有新增功能，只会造成"两个返回相关
    按钮同时存在"的冗余；此改动可逆、風险低，若用户认为应保留请反馈。
  - "外观主题"与"强调色"确认作为左侧两个独立条目（未合并），
    对应用户此前"材质保留为独立设置项"的确认精神——本轮不做任何
    "预设吞并既有选择器"的合并动作。
  - 未新增任何 SQLite 持久化字段，`IAppSettingsRepository` 保持不变。
- Current resume point: 六项 Change checklist 已全部完成，代码层面
  `dotnet build` + `dotnet test` 已通过且无退化。**下一步需要用户在自己的
  机器上手动运行并核对 Acceptance criteria 中标注"需用户手动验证"的各项**——
  本次执行环境无法截图自证。确认通过后可将本 SPEC 转 `[DONE]`；
  若手动验证发现问题，回到本 SPEC 记录 Lessons learned 并继续在
  `in-progress` 状态下修正。
- Subagent/task references: 无。

### 2026-09-26（布局修正：设置占满窗口，选项栏用左侧栏）

- Completed: 用户确认设置不应只占右侧内容区。`MainWindow.axaml` 在
  `IsSettingsOpen` 时把窗口左侧栏换成设置选项列表，最顶部放返回入口；
  右侧只渲染当前选中项。右侧页头的返回和「关于」里的「返回清单」已去掉。
  右上角昼夜切换保留。左侧栏宽度仍为 272。
- Decisions: 用户原话确认上述布局后实施。标签管理已由
  `spec-remove-tag-feature` 移除，选项栏为现有五项。

### 2026-09-26（回填参考站点色板）

- Completed: 用浏览器读取 dogapi.cc 与 linkapi.ai 的主题样式表。两者预设名称与十六进制色值一致。
  `ThemePresets` 改为 9 套命名色板（默认、Anthropic、暗夜、玫瑰花园、湖光、日落霞光、森林低语、海风、薰衣草梦），
  选中时写入窗体、侧栏、卡片、文本、边框、状态色和强调色。启动时应用「默认」。
  选择区改为色板卡片；仅 Anthropic 换成站点衬线栈；非默认主题侧栏朝强调色轻偏。
- Decisions: 用户确认「保留站点的大多数风格内容」；侧栏变色幅度达到期望后要求合入 `dev`。

### 2026-09-26（合入 `dev` 后收紧主题卡片，关闭 SPEC）

- Completed: 合入 `dev` 表示所有者已通过预览。外观主题仍被限制在 640px 两列扁条，
  右侧大块空白。改为三列色板铺满设置内容区，色带高度 88。
- Decisions: 所有者原话「合并到 dev 说明预览没有问题」，此种情况下必须处理 SPEC：
  勾选人工验收项、记录布局修正、将本文件转为 `[DONE]`。
- Current resume point: 本 SPEC 关闭。外观偏好持久化仍属
  `spec-editorial-and-ripple-theme` 的推迟事项，不在本 SPEC 范围。
- Subagent/task references: 无。

---

## Verification

- Automated:
  - `dotnet test FlowTask.sln -c Debug`（合入 `dev` 后，含三列主题卡片）→
    183 通过 / 0 失败 / 0 跳过。
- Manual: 所有者在 Windows 预览包上核对设置页主从布局、命名主题色板、
  Anthropic 衬线字体与非默认侧栏轻染色，原话「效果达到我的期望了，合入dev」。
  合入后又核对主题卡片留白，确认按右侧整栏三列排布。
- Not run or not covered:
  - 主题/强调色/材质选择的持久化——本轮明确不在范围内（见 Constraints）。

---

## Risks and open questions

- **已核销（2026-09-26）**：Anthropic/暗夜/海风等色值已从 dogapi.cc 与 linkapi.ai
  的公开样式表采集并写入 `ThemePresets`。所有者预览后确认效果达标。
- **已核销（2026-09-26）**：主题/强调色/材质选择不持久化。本轮明确不在范围，
  仍由 `spec-editorial-and-ripple-theme` 的推迟事项跟踪。
- **已核销（2026-09-26）**：左侧设置项顺序按既有卷动流迁移；标签项随后被
  `spec-remove-tag-feature` 移除，现为五项。所有者预览后未要求再改顺序。

---

## Lessons learned

合入 `dev` 就是所有者对预览的通过声明。此时必须同步 SPEC：勾选人工验收、
记下合入后的布局修正、把文件改名为 `[DONE]`。只合代码、把 SPEC 留在
`[IN-PROGRESS]` 且验收项仍写「需用户手动验证」，等于工作记忆与已交付状态脱节。

---

## Related documents

- SPECs:
  - `docs/specs/visual-theme/spec-editorial-and-ripple-theme[DONE].md`
    （当前强调色/材质/昼夜切换机制的来源 SPEC，本次在其基础上扩展）
- ADRs: 无
- Rules:
  - `docs/rules/rule-spec-review-gate.md`
  - `docs/rules/rule-no-invented-user-behavior.md`
  - `docs/rules/workflow-methodology.md`
  - `docs/rules/docs-conventions.md`
- Analysis: 无（本次前置调研直接记录在本 SPEC Progress log 中，未产出独立
  `docs/analysis/` 文档，因调研结论已完全内嵌于本 SPEC 且不跨会话复用）。
