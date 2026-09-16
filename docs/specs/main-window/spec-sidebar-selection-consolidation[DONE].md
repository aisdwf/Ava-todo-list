# spec-sidebar-selection-consolidation: 侧边栏选中机制收敛（结构整改）

## Metadata

- **ID**: spec-sidebar-selection-consolidation
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-15
- **Last Updated**: 2026-09-15

> ## ⛔ 开工前置条件（rule-spec-review-gate）
>
> **本 SPEC 状态为 `draft`，未获用户显式确认前严禁改动 `src/` 与 `tests/`。**
>
> 执行者必须先向用户呈现 §3 的执行计划并**停下等待确认**，
> 收到确认后方可将 Status 改为 `in-progress` 并开工。
> 沉默、未回复、或用户在谈论其他话题均不构成确认。
>
> 此约束源于真实事故：spec-task-contract-and-clock 与 spec-classification-ui 均跳过审核直接开工，
> 导致整轮返工。详见 [rule-spec-review-gate](../../rules/rule-spec-review-gate.md)。

**上游依据**：
- 需求：[REQUIREMENTS](../../requirements/REQUIREMENTS.md) R-2.3
- 设计：[design-interaction-principles](../../archived/design-main-window-rework-superseded.md) §1.3 / §7.1（Draft，但本 SPEC 只承接其中**不含产品决策**的结构整改部分）

---

## 0. 新会话接手须知（上下文交接）

> 本节专为**新会话无损接手**而写。执行前请完整阅读。

### 0.1 必读文档（按顺序）

1. `AI_CONSTITUTION.md` —— 项目最高约束，必须先读并声明遵守
2. `docs/rules/README.md` —— 改动前 30 秒防呆清单（含 7 条）
3. `docs/rules/rule-spec-review-gate.md` —— **本 SPEC 的开工门禁**
4. `docs/rules/rule-no-invented-user-behavior.md` —— 严禁自造用户行为假设
5. `docs/design/design-main-window-rework-superseded.md` §1.3 §7.1 —— 本 SPEC 的设计依据
6. 本文件

### 0.2 项目当前状态

- **git**：已建立基线提交 `a7f9a47`（62 文件）。`docs/` 与 `res/` 已被 `.gitignore` 排除。
- **构建**：`dotnet build FlowTask.sln` → 0 警告 0 错误
- **测试**：`dotnet test` → **163 通过 / 0 失败**（这是不得退化的基线）
- **构建命令**（macOS，须带环境变量）：
  ```bash
  export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
  export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
  dotnet build FlowTask.sln -v q --nologo
  dotnet test FlowTask.sln --nologo -v q
  ```
- **启动**：`./run.sh`（但**不得**以「启动不崩」当作功能验证）

### 0.3 关键代码位置（已核实的行号，改动前请复查）

| 文件 | 行 | 内容 |
| :--- | :--- | :--- |
| `src/FlowTask.Desktop/ViewModels/MainViewModel.cs` | 941 行 | 主视图模型，本 SPEC 的主战场 |
| 同上 | 51 | `_isSyncingFilter` 抑制回环标志 |
| 同上 | 72 | `_selectedProject`（项目筛选状态） |
| 同上 | 86 | `_currentFilter`（VIEWS 筛选状态） |
| 同上 | 221 | `LoadProjectsAsync` |
| 同上 | 254 | `OnCurrentFilterChanged` |
| 同上 | 288 | `SelectProjectAsync` |
| 同上 | 329 | `ReturnToActiveViewAsync`（spec-classification-ui 为修 bug 而加） |
| 同上 | 347-351 | 三个 `OnIsXxxFilterSelectedChanged` |
| 同上 | 356 | `SyncFilterFromRadio` |
| 同上 | 378 | `ChangeFilter` |
| 同上 | 388 | `LoadTasksAsync`（筛选分支在此） |
| 同上 | 730 | `SyncProjectSelectionFlags` |
| 同上 | 815 / 872 | 归档 / 删除项目中的 `wasSelected` 处理 |
| `src/FlowTask.Desktop/Views/MainWindow.axaml` | 604 行 | 43-70 为 VIEWS 三项，80-160 为 PROJECTS 区 |
| `src/FlowTask.Desktop/ViewModels/ProjectItemViewModel.cs` | — | `IsSelected` 布尔量在此 |
| `src/FlowTask.Desktop/Views/MainWindow.axaml.cs` | 尾部 | `OnProjectRowTapped` / `OnProjectRowDoubleTapped` |

---

## 1. Why（问题与背景）

### 1.1 病灶：侧边栏存在两套并行的选中机制

| 区域 | 实现方式 | 状态载体 |
| :--- | :--- | :--- |
| VIEWS（全部/今日/已完成） | `RadioButton` + `IsChecked` 双向绑定 | `TaskFilter` 枚举 + 三个 `bool` |
| PROJECTS（各项目行） | `Border` + `Tapped` 事件 | `Project?` + 每行一个 `bool IsSelected` |

二者必须互斥（同时只能有一个生效），但**互斥靠手工在对方的处理逻辑里清零来维持**：

- `OnCurrentFilterChanged`（254 行）里 `SelectedProject = null`
- `SelectProjectAsync`（288 行）里把三个 `IsXxxFilterSelected` 置 false
- `SyncProjectSelectionFlags`（730 行）逐行刷 `IsSelected`

### 1.2 该结构已两次产出同类缺陷

**第一次（spec-editorial-and-ripple-theme）**：用户停留在设置页时点击**当前已选中**的导航项，
界面无任何响应。根因：`CurrentFilter` 赋同值 → 属性不变更 →
`OnCurrentFilterChanged` 不触发 → 设置页不关闭。
修法：在 `ChangeFilter`（378 行）里显式 `IsSettingsOpen = false`。

**第二次（spec-classification-ui）**：删除当前选中的项目后，侧边栏三个 VIEWS 单选
**全部未选中**，出现「什么都没选中」的空档。根因完全相同：
项目筛选期间 `CurrentFilter` 仍停留在 `Active`，删除后执行
`CurrentFilter = TaskFilter.Active` 属于赋同值，回调不触发，高亮无从恢复。
修法：抽出 `ReturnToActiveViewAsync`（329 行）显式重建全部状态。

**两次都是「打补丁」而非改结构。** 补丁位置不同，根因同一个：
**把状态同步的责任交给属性变更通知，而该通知在新值等于旧值时不触发。**

### 1.3 为什么现在整改

design-interaction-principles 将带来大量侧边栏改动（信息架构重构、设置区、标签管理入口）。
**在双机制结构上叠加新功能，几乎必然产出第三次同类缺陷。**
先收敛结构，再动 UI，可为后续 SPEC 清除障碍。

**Attribution**：`Design Wrong` —— 项目筛选被设计为与 VIEWS 筛选并列的
第二套独立状态，而非同一状态的另一个取值。

---

## 2. What（范围与边界）

### 2.1 目标

将「当前查看什么」收敛为**单一状态量**，消除手工互斥清零的必要。

**核心设计**：引入统一的选中标识，使 VIEWS 与项目成为同一枚举/标识的不同取值。

**[推断]** 建议形态（执行时可依实际情况调整，但须保持「单一状态量」这一本质）：

```csharp
/// 当前视图选择。VIEWS 与项目共用同一状态量，
/// 使「同时只能选中一个」成为类型层面的保证，而非靠手工清零维持。
public readonly record struct ViewSelection
{
    public ViewSelectionKind Kind { get; }   // Active / Today / Completed / Project
    public string? ProjectId { get; }        // 仅 Kind == Project 时有值
}
```

配套：每个侧边栏行（含 VIEWS 与项目）统一以
`IsSelected => CurrentSelection == 本行对应的 selection` 表达高亮。

### 2.2 严格非目标（本段绝不做）

| 不做 | 理由 |
| :--- | :--- |
| **任何交互行为的改变** | 本段是**纯结构整改**。用户点击后看到什么、跳转到哪，须与改动前**逐一致** |
| 侧边栏信息架构重构（层级化/分组化） | 侧边栏信息架构（design-interaction-principles §7.2，方案待裁决） 待裁决，属产品决策 |
| 设置区、标签管理入口 | 设置区（design-interaction-principles §7.1），待其批准 |
| 顶部常驻配置区 | design-interaction-principles 的「配置常驻」原则，独立 SPEC |
| 标签实体化 | design-interaction-principles §4（严禁强制手动输入），独立 SPEC |
| 完成任务后的反馈改进 | 完成反馈（design-interaction-principles §7.3） 待裁决 |
| 移除 `EditPanel` 行内编辑态 | 属交互改变，随顶部配置区那一段做 |
| 删除 `Class1.cs` 模板残留 | 与本段意图无关（Article 10 禁止拼凑式修改） |

> **本段的验收核心是「行为零变化」**：163 个既有测试必须全部通过，
> 且不得为迁就新结构而修改任何**断言**（允许修改因 API 签名变化而需要的调用形式）。

### 2.3 影响的文件

> 以下为实施后的实际清单（原计划清单见执行记录 Git 历史，二者有两处出入，
> 已在此按 Article 1 更新为实际情况而非保留预测）：

**实际修改**：
- `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`（核心）
- `src/FlowTask.Desktop/Views/MainWindow.axaml`（VIEWS 三项改为 `Button + Command`）
- `src/FlowTask.Desktop/Styles/EditorialStyles.axaml`（`RadioButton.NavPill` 样式选择器改为
  `Button.NavPill`，`:checked` 伪类改为 `.Selected` 类；**计划阶段未预见到样式层需要同步改动**）
- `tests/FlowTask.Tests/MainViewModelTests.cs`、`ProjectInteractionTests.cs`（适配签名 + 补充回归测试）

**实际新增**：
- `src/FlowTask.Desktop/ViewModels/ViewSelection.cs`（采纳 §2.1 形态）

**计划中列出但实际未改动**：
- `src/FlowTask.Desktop/ViewModels/ProjectItemViewModel.cs` —— `IsSelected` 属性结构未变，
  仍是 `[ObservableProperty] bool`；变化的只是赋值来源（现由 `MainViewModel.SyncProjectSelectionFlags()`
  统一从 `CurrentSelection` 派生），无需改动该文件本身
- `src/FlowTask.Desktop/Views/MainWindow.axaml.cs` —— 项目行的 `Tapped` 事件处理逻辑
  未变（仍调用 `SelectProjectCommand`），本轮整改的是 VIEWS 侧而非项目行侧的触发方式

---

## 3. 分阶段实施计划（须先呈现给用户确认）

- [x] **Phase 1: 引入统一选中状态**
  - [x] 定义 `ViewSelection`（`src/FlowTask.Desktop/ViewModels/ViewSelection.cs`，record struct）
  - [x] `MainViewModel` 以其替代 `CurrentFilter` + `SelectedProject` 双状态（新增 `CurrentSelection`）
  - [x] 保留 `CurrentFilter`、`SelectedProject`、三个 `IsXxxFilterSelected` 作为派生只读视图
- [x] **Phase 2: 统一侧边栏高亮表达**
  - [x] VIEWS 三项由 `RadioButton.IsChecked` 双向绑定改为 `Button + Command + Classes.Selected`
        （与项目行同一表达方式，样式选择器同步改为 `Button.NavPill`）
  - [x] `ProjectItemViewModel.IsSelected` 改为由 `SyncProjectSelectionFlags()` 从 `CurrentSelection` 派生赋值
  - [x] 移除 `_isSyncingFilter`、`SyncFilterFromRadio`、`OnIsActiveFilterSelectedChanged` 等三个 partial 回调
- [x] **Phase 3: 消除手工互斥清零**
  - [x] `ReturnToActiveViewAsync` 简化为单次赋值 `CurrentSelection = ViewSelection.Active`
  - [x] 归档/删除项目中的 `wasSelected` 分支保留判断但去除了对 `ReturnToActiveViewAsync` 的重复分支调用，
        统一交由 `LoadProjectsAsync` 内的存续性检查触发回退
  - [x] `LoadTasksAsync` 的筛选分支改为对 `CurrentSelection.Kind` 做单次判断
- [x] **Phase 4: 回归验证**
  - [x] 163 个既有测试全部通过，**断言零修改**（`git diff` 已核验，仅 2 处调用形式变化，见执行记录）
  - [x] 补测试：赋同值场景 —— `SelectProject_SelectingSameProjectTwice_StaysHighlighted`、
        `ChangeFilter_ToSameValue_StillLeavesSettingsView`
  - [x] 补测试：VIEWS 与项目互斥的不变量 —— `ExactlyOneSelectionIsActive_AcrossAllTransitions`、
        `SelectProject_SwitchingBetweenTwoProjects_MovesHighlightCorrectly`
- [x] **Phase 5: 文档同步**
  - [x] 更新本 SPEC 的执行记录、验证记录与索引表状态
  - [ ] 按 Article 1 规范提交（`Why:` / `What:`）—— 等待用户指示提交时机（AI_CONSTITUTION 职责边界）

---

## 4. 执行记录与上下文追踪

### Subagent Log

| Timestamp | Subagent | Task | Task ID | Outcome |
| :--- | :--- | :--- | :--- | :--- |
| — | — | 暂未派发 | — | — |

### 关键决策与状态增量

- **[2026-09-15]** SPEC 建立，状态 `draft`，**等待用户审核**（rule-spec-review-gate）。
- **[2026-09-15]** 已核实两次同类缺陷的具体位置与修法（见 §1.2），
  确认二者根因同一、均为打补丁而非改结构。
- **[2026-09-15]** 用户显式确认「可以开始了」，状态由 `draft` 转 `in-progress`，开工。

<!-- 以下按 Phase 实际完成情况追加，严禁提前填写（rule-spec-review-gate §2.3）-->

### Phase 执行记录

- **[2026-09-15] Phase 1 完成**：新增 `ViewSelection.cs`（`ViewSelectionKind` 枚举 + `readonly record struct`）。
  `MainViewModel` 新增 `[ObservableProperty] CurrentSelection`，用
  `[NotifyPropertyChangedFor]` 级联通知 `CurrentFilter` / `SelectedProject` /
  三个 `IsXxxFilterSelected`。四者均改为只读派生属性（`=>` 表达式），
  不再是各自独立的 `[ObservableProperty]`。
- **[2026-09-15] Phase 2 完成**：`EditorialStyles.axaml` 的 `RadioButton.NavPill` 样式选择器
  改名为 `Button.NavPill`，`:checked` 伪类选择器改为 `.Selected` 类选择器。
  `MainWindow.axaml` 的 VIEWS 三项从 `RadioButton IsChecked TwoWay` 改为
  `Button Command="{Binding ChangeFilterCommand}" CommandParameter="{x:Static vm:TaskFilter.Xxx}"`，
  高亮经 `Classes.Selected="{Binding IsXxxFilterSelected}"` 单向绑定。
  `_isSyncingFilter` 字段与 `SyncFilterFromRadio`、三个 `OnIsXxxFilterSelectedChanged`
  partial 回调、`OnCurrentFilterChanged` partial 回调均已删除。
- **[2026-09-15] Phase 3 完成**：`ReturnToActiveViewAsync` 从原来的「清空 3 个 bool + 赋值
  `CurrentFilter` + 手工同步」简化为「赋值 `CurrentSelection = ViewSelection.Active` + 加载」两步。
  `ArchiveProjectAsync` / `ConfirmDeleteProjectAsync` 中的 `wasSelected` 判断改为直接比对
  `CurrentSelection.Kind == Project && CurrentSelection.ProjectId == target.Id`，
  两处不再各自调用 `ReturnToActiveViewAsync`，该回退统一由 `LoadProjectsAsync`
  内「选中项目是否仍在新集合中」的检查触发（Article 10：同一决策只在一处表达）。
- **[2026-09-15] Phase 4 完成**：执行 `dotnet build FlowTask.sln -v q --nologo`，
  实际输出「已成功生成。0 个警告 0 个错误」。执行 `dotnet test FlowTask.sln --nologo -v q`，
  实际输出「已通过! - 失败: 0，通过: 167，已跳过: 0，总计: 167」
  （163 基线 + 本轮新增 4 个回归测试）。`git diff tests/` 核验：仅
  `MainViewModelTests.cs` 两处将 `vm.IsXxxFilterSelected = true` 改为
  `vm.ChangeFilterCommand.Execute(TaskFilter.Xxx)`（因该属性已变为只读派生属性，
  原直接赋值的调用形式不再存在），未改动任何 `Assert.*` 行。
  新增测试见 `ProjectInteractionTests.cs`「结构整改回归防护」小节。
- **[2026-09-15] Phase 5 完成**：本节记录与 §5 验证记录已按实际结果填写；
  用户完成人工验证并确认「暂时没有遇到上述问题，可以标记done」（详见 §5 人工验证）；
  状态由 `in-progress` 转 `done`；索引表同步更新见 `docs/specs/README.md`；
  提交动作仍等待用户指示时机（AI_CONSTITUTION 职责边界，本 SPEC 内不自行提交）。

---

## 5. 验证记录

### 机器门禁

- [x] 构建：`dotnet build FlowTask.sln`（基线 0 警告 0 错误，不得退化）——
      实测：0 警告 0 错误
- [x] 测试：`dotnet test`（基线 **163 通过**，不得退化）——
      实测：**167 通过 / 0 失败**（163 基线 + 4 个本轮新增回归测试）
- [x] **断言零修改核验**：`git diff` 检查测试文件，
      确认只改了调用形式、未改任何 `Assert.*` 的预期值 ——
      实测：仅 2 处调用形式变化（详见 §4 Phase 4 执行记录），零处 `Assert.*` 改动

### 人工验证（由用户执行）

本段为纯结构整改，**用户视角应完全无感**。以下清单由用户核验，AI 不得代为勾选。

> **验证记录**：用户于 2026-09-15 confirm「暂时没有遇到上述问题，可以标记done」。
> **如实说明验证深度**：该确认是对 AI 在对话中列出的整体风险点（尤其第 4/5/6 项 ——
> 历史上出过 bug 的场景）的整体反馈，并非逐项打勾式的独立核验记录。
> 按用户明确授权在此标记完成，但不虚构成"逐项走过一遍"的更高精度记录。

- [x] 点击「全部任务 / 今日聚焦 / 已完成归档」→ 高亮与列表切换正常
- [x] 点击某项目 → 高亮切到该项目，VIEWS 三项高亮解除
- [x] 从项目切回 VIEWS → 项目高亮解除
- [x] 停留设置页时点击**当前已选中**的导航项 → 能回到任务流（spec-editorial-and-ripple-theme 缺陷不复现）
- [x] 删除当前选中的项目 → 自动回到「全部任务」且**该项高亮**（spec-classification-ui 缺陷不复现）
- [x] 归档当前选中的项目 → 同上
- [x] 双击项目名重命名 → 仍正常，不触发筛选切换

---

## 6. Deferred Items（显式追踪）

承接自前序 SPEC，本段不处理：

- **TODO(archive-ui): [2026-10-12]** 项目归档命令无 UI 入口 → 设置区（design-interaction-principles §7.1） 将移入设置区
- **TODO(desc-field): [2026-10-05]** `Description` 死字段 → design-interaction-principles §4（严禁强制手动输入）.3.2 已决定废弃，待确认是否物理删列
- **TODO(recycle-bin): [2026-10-05]** 软删除任务无恢复入口
- **TODO(appearance-persist): [2026-09-28]** 外观偏好重启后回退默认
- **TODO(cleanup): [2026-09-21]** `Class1.cs` 模板残留（Core / Infrastructure 各一）

---

## 7. Commit Attribution 与经验教训

- **Attribution**: `Design Wrong` —— 项目筛选被设计为独立的第二套状态，
  而非同一状态的另一取值，导致互斥必须手工维护。
- **Root Cause**: 见 §1.2。同一根因两次产出缺陷，两次均打补丁未改结构。
- **Lessons Learned**: 见 §8。

---

## 8. Lessons Learned

<!-- 执行过程中如实追加，严禁预先编写（rule-spec-review-gate §2.3）-->

- **[2026-09-15]** 大段替换 `MainViewModel.cs` 顶部字段块时，误连带删除了
  `CurrentCategoryTitle` / `CurrentCategorySubtitle` 两个与本次整改无关的
  `[ObservableProperty]` 字段（编辑范围过大导致误删相邻代码）。
  `dotnet build` 立即报 `CS0103` 当场拦获，修复方式是原样补回这两个属性。
  **教训**：多字段声明连续排列时，改动前应更精确地框定 oldString 边界，
  避免用"看起来在同一块"的大范围替换覆盖不该动的相邻声明；
  本例因构建门禁及时拦截未造成实质影响，记录以提醒后续复用同类大范围编辑手法时收紧范围。
