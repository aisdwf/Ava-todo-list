# spec-classification-ui: 主窗口分类交互与校验值对象

## Metadata

- **ID**: spec-classification-ui
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-15
- **Last Updated**: 2026-09-15

**上游依据**：
- 需求：[REQUIREMENTS](../../requirements/REQUIREMENTS.md) R-2.1 / R-2.2 / R-2.3 / R-2.4 / R-2.5
- 设计：[design-domain-contract](../../archived/design-task-classification-superseded.md)（Approved）§5
- 前置：[spec-task-contract-and-clock](../task-domain/spec-task-contract-and-clock[DONE].md)（`done`）已完成契约层

**本 SPEC 是 design-domain-contract 三段实施拆分中的第 2 段。**
第 3 段（小窗改造）另立 SPEC。

---

## 1. Why（问题与背景）

### 1.1 当前处于「契约齐备但无界面可达」的尴尬状态

spec-task-contract-and-clock 产出了 `Project` 实体、`ProjectId` / `Tags` 字段、
`CommitTaskEdit` / `AssignProject` / `SetDueDate` 三个命令，
以及 `SqliteProjectRepository`。**但没有任何界面能触达它们**：

- `App.axaml.cs` 未装配 `IProjectRepository`（spec-task-contract-and-clock 的执行记录 显式记录的偏离）
- `MainWindow.axaml` 无项目侧边栏、无编辑入口、任务行不显示分类信息
- 用户实际打开应用，看到的与 spec-task-contract-and-clock 之前完全一致

这是分段实施的预期中间态，但**必须由本段消除**，否则前一段的产出等于零价值。

### 1.2 外部对照暴露的差距

与同源工作流项目 `xin-07/TodoList` 对照（同样基于
`AI_CONSTITUTION.md` + SPEC 驱动，创建时间相近），
该项目已具备：标题原地编辑（双击 / F2）、文件夹分类归属、
截止日期严格校验、实时搜索过滤。而我们对应能力全部停留在 ViewModel 层。

> **对照的局限（如实声明）**：当前网络下 `github.com` 与
> `raw.githubusercontent.com` 不可达，仅 `api.github.com` 可用，
> 故该项目**未被 clone、未跑构建与测试**。
> 上述判断基于其源码与 README 阅读，非实测验证。

### 1.3 该项目值得借鉴的两处做法

**一、校验规则以值对象承载。** 它把标题校验（Trim + 非空 + 上限）
与日期格式（常量 + 严格解析）抽为独立静态类，Repository 与 ViewModel 共用同一份。

我们当前的标题校验散落三处 —— `MainViewModel.AddTaskAsync`、
`MainViewModel.CommitTaskEditAsync`、`QuickCaptureViewModel.SaveAsync`，
且各处规则不完全一致（创建路径只判空白，编辑路径判空白 + 恢复原值，
**无任何长度上限**）。这是靠注释约定而非类型保障的单一真源，属 Article 6 的薄弱点。

**二、集合以只读投影暴露。** 它的 `Items` 是 `ReadOnlyObservableCollection`，
接口注释明确「不允许外部 Add/Remove」，以类型强制而非文档约定。

我们的 `MainViewModel.Tasks` 是可写 `ObservableCollection`，
任何代码都能绕过仓储直接改集合，使「仓储是唯一写入漏斗」这一约束可被静默违反。

**Attribution**：`N/A (Feature)` 为主，叠加 `Design Wrong`（校验规则未收敛为单一真源）。

---

## 2. What（范围与边界）

### 2.1 目标

- **校验值对象**：抽出 `TaskTitle`（Trim + 非空 + 上限）与 `ProjectName`，
  三处创建/编辑路径统一复用
- **装配项目仓储**：`App.axaml.cs` 注入 `IProjectRepository`
- **项目侧边栏**：项目列表 + 计数 + 新建 / 重命名 / 改色 / 归档 / 删除
- **项目筛选**：与既有 VIEWS 筛选正交，不塞入 `TaskFilter` 枚举
- **任务行分类呈现**：项目色条、标签、deadline（逾期强调）
- **编辑态**：行内展开，含标题、项目、标签、deadline、优先级
- **零分类体验保真**：无项目时侧边栏项目区整块隐藏，任务行布局与当前一致

### 2.2 非目标（本段明确不做）

| 不做 | 归属 |
| :--- | :--- |
| 小窗列表、勾选、键盘导航、拖拽区改造 | 第 3 段 SPEC |
| 实时搜索过滤 | 未纳入 REQUIREMENTS，需先确认是否作为需求 |
| 桌面通知提醒 | REQUIREMENTS §6 已排除（需后台调度器） |
| 全局异常落盘日志 | 未纳入 REQUIREMENTS |
| 输入语法 `#项目` `@标签` | design-domain-contract §5：初版不做 |
| 启用 `Description` | 承 spec-task-contract-and-clock 的推迟事项，仍为显式待办 |
| 回收站视图 | 未纳入 REQUIREMENTS |
| 打包整改（R-3） | design-domain-contract：置于功能完成之后 |
| 项目层级、任务属多项目、标签重命名 | design-domain-contract §4 已排除 |

### 2.3 影响的文件与模块

**新增**：
- `src/FlowTask.Core/Models/TaskTitle.cs`（校验值对象）
- `src/FlowTask.Core/Models/ProjectName.cs`（校验值对象）
- `src/FlowTask.Desktop/ViewModels/ProjectItemViewModel.cs`（项目行状态）
- `src/FlowTask.Desktop/Converters/DueDateConverters.cs`（到期文案与逾期判定）
- `tests/FlowTask.Tests/ValidationTests.cs`
- `tests/FlowTask.Tests/ProjectInteractionTests.cs`

**修改**：
- `src/FlowTask.Core/Models/TaskItemFactory.cs`（复用 `TaskTitle`）
- `src/FlowTask.Infrastructure/Persistence/SqliteTaskRepository.cs`（写入侧复用校验）
- `src/FlowTask.Infrastructure/Persistence/SqliteProjectRepository.cs`（复用 `ProjectName`）
- `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`（项目状态、筛选、编辑态、只读集合）
- `src/FlowTask.Desktop/ViewModels/QuickCaptureViewModel.cs`（复用 `TaskTitle`）
- `src/FlowTask.Desktop/Views/MainWindow.axaml`（侧边栏项目区、任务行、编辑态）
- `src/FlowTask.Desktop/Views/MainWindow.axaml.cs`（编辑态交互）
- `src/FlowTask.Desktop/Styles/EditorialStyles.axaml`（新样式类）
- `src/FlowTask.Desktop/Styles/Tokens.{Light,Dark}.axaml`（如需新令牌）
- `src/FlowTask.Desktop/App.axaml.cs`（装配项目仓储）

### 2.4 必须遵守的既有契约

| 契约 | 约束 |
| :--- | :--- |
| `TaskFilter` 枚举 | **不得**塞入项目筛选。该枚举曾混入「设置页」导致视图模式与数据筛选耦合，已在 spec-editorial-and-ripple-theme 修正（见 `MainViewModel.cs:16-21` 注释）。项目筛选须另立正交状态 |
| spec-editorial-and-ripple-theme 视觉令牌 | 新增 UI **必须**复用 `Tokens.{Shared,Light,Dark}.axaml` 既有键名。该问题曾导致 spec-editorial-and-ripple-theme 首轮实现视觉全面失效（其 §1.1 根因） |
| `_isSyncingFilter` 防重载 | 新增筛选须接入既有机制，不得另起一套 |
| 零必填原则 | 主输入路径**不新增任何控件**（design-domain-contract §5） |
| 删除项目不删任务 | 已由 spec-task-contract-and-clock 落实并测试覆盖，本段 UI 须正确传达此语义 |

---

## 3. 分阶段实施计划

- [x] **Phase 1: 校验值对象与只读集合收敛**
  - [x] 新增 `TaskTitle`（`MaxLength=200` + `Validate` + `Normalize` + `IsValid`）
  - [x] 新增 `ProjectName`（`MaxLength=50` + `Validate` + `Normalize`）
  - [x] `TaskItemFactory` / 两个 ViewModel / 两个仓储统一复用
  - [x] `MainViewModel.Tasks` 改为 `ReadOnlyObservableCollection`
  - [x] 补测试：校验边界、超长拒绝、仓储层兜底（20 例，累计 133 通过）
- [x] **Phase 2: 装配与项目 ViewModel 层**
  - [x] `App.axaml.cs` 装配 `IProjectRepository`
  - [x] 新增 `ProjectItemViewModel`（承载派生计数与重命名编辑态）
  - [x] `MainViewModel` 承载项目集合、选中项目、项目 CRUD 命令
  - [x] 项目筛选状态 `SelectedProject`（独立于 `TaskFilter`）
  - [x] 补测试：项目筛选正交性、CRUD 落库、删除影响提示（25 例，累计 158 通过）
- [x] **Phase 3: 项目侧边栏 UI**
  - [x] PROJECTS 区块（`HasProjects` 驱动整块隐藏）
  - [x] 新建（渐进披露：默认仅「＋」）/ 双击重命名 / 改色 / 删除入口
  - [x] 删除确认条（含影响任务条数，并明写「任务会保留」）
  - [x] ~~归档入口~~ —— 命令已实现且测试覆盖，但**未接入 UI**，见 §6
- [x] **Phase 4: 任务行分类呈现与编辑态**
  - [x] 项目色条、标签 chip、deadline（逾期/今日分色）
  - [x] 行内展开编辑态（标题/标签/到期日/优先级/所属项目）
  - [x] 零分类时行布局与改动前一致（各元素独立 `IsVisible`）
- [x] **Phase 5: 验证与文档同步**
  - [x] 全量 `dotnet build`（0 警告 0 错误）+ `dotnet test`（163 通过）
  - [x] 样式类与令牌键的存在性逐个核验（防 Avalonia 静默失败）
  - [x] 实机启动验证 + 真实数据库 `Projects` 建表确认
  - [x] 文档同步与索引更新

---

## 4. 执行记录与上下文追踪

### Subagent Log

| Timestamp | Subagent | Task | Task ID | Outcome |
| :--- | :--- | :--- | :--- | :--- |
| — | — | 暂未派发 | — | — |

### 关键决策与状态增量

- **[2026-09-15]** SPEC 建立，状态直接置 `in-progress`（开工铁律）。
- **[2026-09-15]** 已核实现有视觉令牌词汇表，新增 UI 须从中取键：
  - 形状/尺寸：`ControlCornerRadius` `CardCornerRadius` `PillCornerRadius`
    `FontSizeMicro/Caption/Body/TaskTitle/SectionTitle/HeroTitle` `TaskRowPadding` `SidebarPadding`
  - 颜色：`TextPrimary/Secondary/Tertiary/DisabledBrush` `Hairline/HairlineStrongBrush`
    `Accent/AccentSubtle/AccentGlow/OnAccentBrush` `CardSurface/CardSurfaceHoverBrush`
    `PriorityHigh/Medium/LowBrush` 及对应 `*SurfaceBrush` `KeyCapSurface/BorderBrush`
- **[2026-09-15]** 已核实现有样式类：`NavPill` `CountBadge` `TaskRow` `TaskTitle`
  `PriorityTag`(+`P1/P2/P3`) `RingCheck` `IconCircle`(+`Compact`/`RowAction`)
  `EditorialInput` `SegmentChoice` `MicroLabel` `CaptionText` `SectionTitle`
  `BodyText` `HeroTitle` `SettingsCard` `ChoiceGrid` `KeyCap` `CaptureTrigger`

<!-- 以下按 Phase 实际完成情况追加，严禁提前填写 -->

### Phase 执行记录

- **[Phase 1]** `TaskTitle.MaxLength = 200`，`ProjectName.MaxLength = 50`。
  两者的 `Normalize` **刻意不同**：任务标题只修剪首尾（内部字符须原样保留，
  否则未来 `#项目` / `@标签` 语法无从解析）；项目名折叠内部空白
  （它是被反复引用的标识符，「我的 项目」与「我的  项目」视觉无法区分
  却会被当作两个项目）。该差异已写成断言锁定。
- **[Phase 1]** 校验兜底放在**仓储**而非仅 UI 层：仓储是唯一写入漏斗，
  即使某个入口遗漏校验也不让非法数据落库。
- **[Phase 1]** `MainViewModel.Tasks` 改为 `ReadOnlyObservableCollection`
  （借鉴 xin-07/TodoList）。此前任何代码都能绕过仓储直接改集合，
  界面会显示未落库的幽灵数据且编译器无法发现。
- **[Phase 2 · 测试捕获真实缺陷]** `ConfirmDeleteSelectedProject_FallsBackToActiveView`
  首次运行**失败**，暴露一个与 spec-editorial-and-ripple-theme 已修正缺陷**同源**的问题：
  - **现象**：删除当前选中的项目后，侧边栏三个 VIEWS 单选全部处于未选中状态，
    呈现「没有任何项被选中」的空档。
  - **根因**：项目筛选生效期间 `CurrentFilter` 仍停留在原值（通常就是 `Active`），
    仅三个 `IsXxxFilterSelected` 被清空。删除后执行 `CurrentFilter = TaskFilter.Active`
    属于**赋同值** —— 属性不变更、`OnCurrentFilterChanged` 不触发、高亮无从恢复。
  - **这正是 spec-editorial-and-ripple-theme 记录过的模式**：「依赖属性变更回调同步状态，
    在新值等于旧值时必然失效」（见 `ChangeFilter` 的既有注释）。
  - **修正**：抽出 `ReturnToActiveViewAsync` 显式重建全部相关状态，
    不经由属性变更通知这条路径。三处调用点（删除、归档、`LoadProjectsAsync`
    的失效选中清理）统一改用它。
- **[Phase 2 · 类型陷阱]** `AppearanceOption.Swatch` 是 `IBrush`，
  `ToString()` 返回可空且格式不保证是十六进制。改用 `DarkHex`：
  它是真实色值字符串，且避免 ViewModel 层为取一个色值而触达
  UI 线程绑定的 `SolidColorBrush`。此问题由 `TreatWarningsAsErrors=false`
  下的 CS8603/CS8601 警告发现 —— 基线是 0 警告，故未放过。

---

## 5. 验证记录

### 机器门禁

- [x] 构建：`dotnet build FlowTask.sln` — **0 警告 0 错误**
- [x] 测试：`dotnet test` — **163 通过 / 0 失败**（基线 113，本段新增 50 例）
  - 校验值对象与仓储兜底 20 例
  - 项目交互与筛选正交性 25 例
  - 编辑态（缓冲、非法输入、互斥展开、日期严格解析）5 例改写 + 扩充
- [x] 样式类存在性：12 个新类逐个确认已定义
- [x] 令牌键存在性：全部 `DynamicResource` 引用与定义集合差集为空
- [x] 实机启动 + 真实数据库建表验证（事前已备份）

### 人工验证

本段首次产出用户可见界面，因此人工验证是必需的而非可选的。

- [x] 应用启动无异常（实机运行 22 秒，日志无告警）
- [ ] 新建项目 → 侧边栏出现，计数正确
- [ ] 双击项目名 → 原地重命名；Enter 提交、Esc 取消
- [ ] 点击项目行 → 任务列表切到该项目，VIEWS 高亮解除
- [ ] 任务归属项目 → 行左侧出现项目色条
- [ ] 设置 deadline → 「今日聚焦」不再恒为空；逾期显示红色、今天显示强调色
- [ ] 点击任务标题 → 编辑态展开；改标题/标签/日期/优先级/项目后落库
- [ ] 删除项目 → 确认条显示任务条数；确认后**任务仍在**且退回未归属
- [ ] 零分类状态 → 侧边栏无 PROJECTS 区块，任务行布局与改动前一致

> **验证局限（spec-editorial-and-ripple-theme 教训 4）**：编辑态展开、项目切换、删除确认
> 均为必须点击才触发的路径，而脚本化点击在 macOS 上受辅助访问权限限制。
> 本段以 headless 测试覆盖其**代码路径**，
> UI 层的实际渲染与手势响应**必须由用户人工核验**，
> 绝不以「进程存活」冒充功能正常。

---

## 6. Deferred Items（显式追踪）

- **TODO(archive-ui): [2026-10-12] 项目归档命令已实现且测试覆盖，但无 UI 入口。**
  - `ArchiveProjectCommand` 可用，侧边栏仅暴露了改色与删除两个按钮。
  - **为什么推迟**：项目行已有 3 个悬停浮现的操作（改色/删除）加计数徽标，
    再塞一个会让 272px 宽的侧边栏拥挤到失去可用性。
    合理的承载方式是右键菜单或项目详情弹层，属独立的交互设计决策。
  - **当前后果**：用户只能删除项目，无法归档。二者语义不同
    （归档保留任务归属，删除置空），因此这是一项真实的能力缺失。
  - Owner: aisdwf
- **TODO(desc-field): [2026-10-05] `Description` 仍为死字段（承 spec-task-contract-and-clock）。**
  - 编辑态已有五个字段，加备注需要多行文本框，会显著增高面板。
  - Owner: aisdwf
- **TODO(recycle-bin): [2026-10-05] 软删除任务无恢复入口（承 spec-task-contract-and-clock）。**
  - Owner: aisdwf
- **TODO(appearance-persist): [2026-09-28] 外观偏好重启后回退默认（承 spec-editorial-and-ripple-theme）。**
  - Owner: aisdwf

---

## 7. Commit Attribution 与经验教训

- **Attribution**: `N/A (Feature)` + `Design Wrong`（校验规则未收敛为单一真源）
- **Root Cause**：标题校验散落三处且规则不一致（两处只判空白、均无长度上限），
  靠注释约定而非类型保障，属 Article 6 的结构性薄弱点而非单点疏漏。
- **Lessons Learned**: 见 §8。

---

## 8. Lessons Learned

1. **「新值等于旧值时属性变更回调不触发」是本项目的高频缺陷模式。**
   spec-editorial-and-ripple-theme 已修正过一次（导航点击当前视图无响应），本段又踩中一次
   （删除选中项目后侧边栏无任何高亮）。两次的形态不同但根因相同：
   **把状态同步的责任交给属性变更通知**。
   对策是遇到「重置到某个默认状态」的需求时，一律显式重建全部相关状态
   （如本段抽出的 `ReturnToActiveViewAsync`），不依赖赋值触发回调。
   建议将此模式沉淀为项目规则 —— 它已出现两次，符合规则化的门槛。

2. **测试写在实现之后仍然有效，前提是断言基于设计意图而非当前实现。**
   `ConfirmDeleteSelectedProject_FallsBackToActiveView` 是照着
   「删除后应回到全部任务视图」这一设计意图写的，因此立刻抓出了实现缺陷。
   若照着刚写完的代码去写断言，只会固化 bug。

3. **展示态与领域实体必须分离，且这个边界会被 UI 需求反复试探。**
   本段两次不得不引入包装 ViewModel（`ProjectItemViewModel` 承载计数与
   重命名态，`TaskRowViewModel` 承载编辑缓冲与项目色）。
   每次的诱惑都是「直接在实体上加个字段更快」，但那会让
   「保存实体」连带写入 UI 状态。判据很清晰：
   **该值是否需要持久化？不需要就不属于实体。**

4. **框架的作用域共享机制容易造成跨实例串扰。**
   `RadioButton.GroupName` 在同一可视树内共享，列表项模板中若用固定组名，
   所有行会互相取消选中。凡在列表项内使用带「组」概念的控件
   （RadioButton、ToggleGroup 等），组标识必须从数据 Id 派生。

5. **声明式 UI 的静默失败必须用机器手段主动核验，不能靠肉眼。**
   本段新增 12 个样式类与大量 `DynamicResource` 引用。
   Avalonia 对未定义的资源键与未匹配的选择器既不报错也不警告
   （spec-editorial-and-ripple-theme 教训 1 的代价是整轮视觉全部失效）。
   本段收尾时做了两项差集比对，成本极低但能彻底排除该类缺陷。
   **这应当成为凡涉及 XAML 改动的标准收尾动作。**

6. **拒绝非法输入时，粒度应尽可能小。**
   编辑态标题非法时，最初的设计是拒绝整次提交。
   但用户可能同时改了日期与标签 —— 因标题拼错而丢弃全部修改，
   是比「标题没改成功」严重得多的意外损失。
   改为只丢弃非法的那一项。**校验的目的是保护数据，不是惩罚输入。**

7. **外部对照能有效暴露自身盲区。**
   本段的两项改进（校验值对象、只读集合投影）均来自
   `xin-07/TodoList` 的做法。同源方法论下的另一个实现，
   比凭空反思更容易发现「本该如此但一直没做」的地方。
