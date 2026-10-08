# spec-quick-window-single-project-list: 小窗单项目列表 + 勾选

## Metadata

- **ID**: spec-quick-window-single-project-list
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-16
- **Last Updated**: 2026-09-27

> ## ✅ 已关闭（2026-09-27）
>
> Owner 预览 `feature/quick-window-project-linkage` 后回复「没问题」，§7.5 验收通过。
> 同轮发现的「主窗增删改项目后小窗下拉不实时刷新」是独立缺陷，
> 在同一分支以后续提交修复，见 [spec-quick-window-project-sync](./spec-quick-window-project-sync[DONE].md)。

> ## ⚠ 本轮修订（2026-09-27）：下拉成为唯一项目上下文
>
> D1 中「`@` 补全仍保留但只影响新建任务的归属」被推翻：`@` 语法整体移除，
> 新建任务归属当前下拉项目，保存后小窗保持打开，`Ctrl+Tab` 循环切换项目。
> 详见 §7；§3 中涉及 `@`/`#` 补全的表述以 §7 为准。

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-23）在 `feature/quick-window-standalone` 分支按 1→2→3 顺序开工。
> 本 SPEC 为第 3 份，状态 `in-progress`。任务生命周期改由
> `spec-project-managed-tasks` 定义（归档已废除；完成套件 = 置底/划线/降权）。D1/D2 已裁决，见 §2.5。**
>
> **本轮同步解决的另一问题**：主界面此前一直以「随手记」（纯输入胶囊）展示这个小窗入口，
> 与本 SPEC「独立小窗：可查看列表+勾选」的定位不符，文案与视觉需同步更新为「快捷小窗」定位。

**上游依据**：
- 需求：R-1.1 / R-1.2 / R-1.4；展示范围本轮裁决为**单项目**
- 设计：design-interaction-principles §8；完成套件见 spec-project-managed-tasks
- 用户原话：「还要能显示 todo list（单项目）和勾选操作」；「记忆上次项目」

**依赖**：
- 硬依赖（热键唤起）：`spec-quick-window-hotkey-capture`（可先用按钮唤起做 UI，但验收按热键）
- 硬依赖（勾选容错）：`spec-project-managed-tasks`（完成套件；取代 complete-before-archive）

**建议开工顺序中的第 3 份**（先 1 热键捕捉，再 2 完成/归档，再本份列表勾选）。

---

## 0. 新会话接手须知

1. 当前小窗无列表；主窗 `GetActive*` 查询排除 `IsCompleted`（在 archive SPEC 前会改）。
2. 「单项目」= 任一时刻只展示**一个**项目（或「未归属」）下的任务，不做多项目混合列表。
3. 默认项目：**记忆上次在小窗选择的项目**（用户裁决）。持久化位置见 §2.4。

---

## 1. Why

R-1.1 / R-1.2 要求小窗可查看并勾选。用户补充：

- 列表维度为**单项目**（早期演进方向，现升为初版范围）；
- 默认**记忆上次项目**。

Attribution：`Design Incomplete` —— §8 曾推迟小窗；列表范围曾为「全部未完成」推断，现被「单项目」原话取代。

---

## 2. What

### 2.1 窗口结构

- 上：输入区（由 hotkey-capture SPEC 负责语法；本 SPEC 不改解析）
- 下：可滚动任务列表（勾选圈 + 标题 + **[推断]** 项目色条可省略因已在单项目上下文）
- 高度随列表增长，设上限后滚动（**[推断]** 可见约 5–7 行；待试调，不阻塞开工）

### 2.2 列表数据

- 展示当前选中项目下、**未删除**任务（含已完成；样式与排序由 spec-project-managed-tasks 定义）。
- 项目切换：**[待裁决]** 见 D1（快捷键 / 下拉 / 输入 `@` 切换是否联动列表）。
- 槽位为具体项目（含系统 **Default**）；废除 `ProjectId == null`（R-2.6）。

### 2.3 勾选

- 鼠标与键盘均可（R-1.4）；**[推断]** Space 切换当前行完成态。
- 行为必须调用与主窗同一套完成/取消完成命令（完成套件由 spec-project-managed-tasks 保证）。
- **禁止**勾选后立即从列表移除。

### 2.4 记忆上次项目

- 键：如 `AppSettings` 中 `QuickWindow.LastProjectId`（空串 = 未归属）。
- 小窗打开时恢复；用户切换项目时写入。

### 2.5 已裁决（2026-09-23）

| # | 议题 | 裁决 |
| :--- | :--- | :--- |
| D1 | 项目切换 UI | **A**：输入区下方加一个下拉（`ComboBox`），候选为 `GetActiveProjectsAsync()` 结果（含 Default）。选中即联动列表重新查询，不做快捷键循环（B）、不强制通过 `@` 间接切换（C）——`@` 补全仍保留但只影响新建任务的归属，不切列表焦点，避免输入语法与列表状态耦合导致的意外跳变 |
| D2 | 列表排序 | 与主窗共用 `TaskListOrder`（spec-project-managed-tasks）：未完成在上已完成置底；优先级 → 截止 → 创建；已完成再按 `CompletedAt` |

**范围收窄说明**：本轮不做「可见行数上限 + 内部滚动裁剪」的精确调参（SPEC 原文 §2.1 提到 5–7 行待试调）；改为窗口整体设 `MaxHeight`，内部用 `ScrollViewer` 兜底，具体行数留给人工验收时目测调整，不阻塞开工。

### 2.6 非本 SPEC

- 全局热键、`@` 解析 → hotkey-capture
- 任务删除 / 完成套件权威定义 → spec-project-managed-tasks
- 多项目总览 / 今日聚焦切片 → 今日聚焦已于 2026-10-01 关闭为不做（due-date SPEC 的 TODO(quick-capture-today)）

---

## 3. How（Staged plan）

1. **IAppSettingsRepository**：无需新增接口方法，复用既有 `GetAsync`/`SetAsync` 键值对存取，键名 `QuickWindow.LastProjectId`（空串或缺失 = Default）。
2. **QuickCaptureViewModel** 扩展：
   - 新增 `ObservableCollection<TaskRowViewModel> Tasks`（直接复用主窗 `TaskRowViewModel`，避免重复建一套行包装）。
   - 新增 `ObservableCollection<ProjectItemViewModel> Projects`（下拉候选）与 `ProjectItemViewModel? SelectedProject`（D1：下拉绑定）。
   - `PrepareAsync` 扩展：载入项目列表；从 `IAppSettingsRepository` 读回上次项目 Id，若该项目已不存在则回落 Default；载入该项目任务并按 D2 排序分组。
   - `partial void OnSelectedProjectChanged`：切换时写回 `QuickWindow.LastProjectId`，重新查询任务列表。
   - 新增 `ToggleTaskCompleteCommand`：复用 `ToggleCompleteTaskViewModel`（与主窗同一套完成语义，不重新实现）。
   - `SaveAsync` 成功新建任务后，若任务归属的项目就是当前 `SelectedProject`，直接把新任务插入 `Tasks`（或整体重新查询一次，取实现简单者），使新增内容立即可见，不需要关闭再重开小窗才能看到。
3. **QuickCaptureWindow.axaml** 布局改造：
   - 输入区（含 `@`/`#` 补全）保持不变，置于顶部。
   - 输入区下方新增项目下拉（`ComboBox`，绑定 `Projects`/`SelectedProject`）。
   - 下拉下方新增任务列表（`ItemsControl` + `ScrollViewer`），每行：勾选圈（`CheckBox` 绑定 `Task.IsCompleted`，`Command` 走 `ToggleTaskCompleteCommand`）+ 标题（复用 `.Completed` 样式类做划线低饱和）。不显示项目色条（已在单项目上下文，见 §2.1 推断）。
   - 窗口整体 `SizeToContent`/`MaxHeight` 调整：原 620×220 的固定高度不再适用列表可变长内容，改为 `Height="Auto"` + 内容区 `MaxHeight`，超出内部滚动。
   - 处理拖拽热区冲突：复用现有 `IsDescendantOfListBox` 排除模式（`QuickCaptureWindow.axaml.cs` 已有 `PointerPressed` 整窗拖拽 + 排除 `ListBox`/`ListBoxItem` 的写法），新任务列表容器同样要排除，否则点击任务行会触发整窗拖拽。
4. 键盘：`Esc` 隐藏（沿用现有逻辑）；本轮**不**新增「上下选中行 + Space 勾选」的纯键盘操作行（D 待验证，勾选主要靠鼠标点击圈；R-1.4「全程无鼠标」的完整覆盖登记为 Deferred，因为要新增列表内的行焦点管理，属于较大的独立改动）。
5. `dotnet build` + `dotnet test`。

---

## 4. 验证

### 机器

```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
dotnet build FlowTask.sln -v q --nologo
dotnet test  FlowTask.sln --nologo -v q
```

- [x] `dotnet build` 零警告零错误（AI 已跑）
- [x] `dotnet test` 全绿（195 通过，含本轮新增 5 项 `QuickCaptureViewModelTests`）
- [x] `QuickCaptureViewModel` 新增测试覆盖：`PrepareAsync_DefaultsToDefaultProjectWhenNoMemoryExists` / `PrepareAsync_LoadsTasksForRestoredProject` / `SelectedProjectChanged_ReloadsTasksAndPersistsMemory` / `ToggleTaskComplete_KeepsTaskVisibleAndStrikesThrough` / `SaveAsync_RefreshesListWhenNewTaskMatchesSelectedProject`

### 人工（你执行；AI 不得代勾，全部完成后统一在 Windows 上测）

| # | 操作 | 应看到的现象 | 通过？ |
| :--- | :--- | :--- | :--- |
| Q1 | 打开小窗 | 输入框下方出现项目下拉与任务列表（不再是纯输入胶囊） | [ ] |
| Q2 | 切换下拉到另一个项目 | 列表刷新为该项目下的任务 | [ ] |
| Q3 | 在列表中点击某行的勾选圈 | 该行划线低饱和，**仍在列表中并置底** | [ ] |
| Q4 | 再次点击同一勾选圈 | 恢复未完成样式 | [ ] |
| Q5 | 关闭小窗后用热键重新打开 | 下拉恢复为上次选择的项目，不是每次都回到 Default | [ ] |
| Q6 | 重启应用后再打开小窗 | 下拉仍恢复为上次项目（持久化跨进程生效） | [ ] |
| Q7 | 在输入框打 `买菜 @某项目` 后回车 | 若当前下拉选中的正是「某项目」，新任务立即出现在列表里，不需要重开小窗 | [ ] |
| Q8 | 全程不打开主窗，只用小窗完成 Q1-Q4 | 验证 R-1.4「不打开主窗口即可查看与勾选」 | [ ] |

---

## 5. Deferred Items

| 事项 | 期限 | 触发条件 |
| :--- | :--- | :--- |
| ~~TODO(quick-capture-today) 各项目+今日~~ | 已关闭（2026-10-01，不做） | 见 [spec-close-deferred-todos](../docs-system/spec-close-deferred-todos[DONE].md) |
| 多项目切换总览 | 演进 | 用户再次要求 |
| 列表内纯键盘操作（上下选中行 + Space 勾选，无需鼠标） | 2026-10-31 | 用户反馈鼠标依赖影响使用体验 |
| 可见行数上限精确调参（当前用 MaxHeight 兜底） | 2026-10-15 | 人工验收后目测调整 |

---

## Progress log

### 2026-09-23

- Completed：`QuickCaptureViewModel` 扩展项目下拉（`Projects`/`SelectedProject`）、单项目任务列表（`Tasks`，复用主窗 `TaskRowViewModel`）、`ToggleTaskCompleteCommand`（复用 `ToggleCompleteTaskViewModel`）、`ChangeSelectedProjectCommand`（写记忆项+重新查询）；`QuickCaptureWindow.axaml` 从 620×220 固定胶囊改为 `SizeToContent="Height"` + 项目下拉 + 可滚动任务列表；拖拽热区排除范围扩展到 `ComboBox`/`CheckBox`/`ScrollViewer`；主窗按钮文案由「随手记」改为「快捷小窗」，副标题改为「查看待办、勾选、随手记一件事」；新增 `QuickCaptureViewModelTests.cs`（5 项测试）。
- Decisions：D1（下拉切换）/D2（未完成在上、已完成置底）按 §2.5 裁决执行；范围收窄为不做纯键盘选中行（登记 Deferred）。
- Current resume point：机器验证已通过（195 测试全绿）；等待用户在 §4 人工验证清单上做 Windows 端统一测试（与 SPEC-1/SPEC-2 一起）。
- Subagent/task references：无（本轮由主会话直接实现）。

---

## 6. Lessons Learned

### 2026-09-27 两条归属路径并存导致联动断裂

**Attribution**：`design wrong` —— D1 为避免「输入语法与列表状态耦合」，让下拉只控制列表、`@` 只控制归属。
结果下拉选中 A 时直接输入标题，任务落入 Default、不出现在当前列表；保存后又立即关窗，用户看不到新任务去了哪里。
「解耦」解的是两个本应是同一事实（当前项目上下文）的状态，违反 Article 6 单一真源。

---

## 7. 本轮修订：项目上下文联动（2026-09-27）

### 7.1 Why

用户原话（2026-09-27）：

> 「1.展示的文字过时，现在已经去除了标签的概念
> 2.在新的项目绑定的内容下，联动性做的不好，@#的形式完全过时了」

现状核实：
- 占位文字 `灵感…  @项目  #标签`，标签已于 spec-remove-tag-feature 移除。
- `SaveAsync` 归属只看 `@`，无 `@` 回落 Default，与下拉无关（见 §6）。
- 保存后 `RequestClose`，新任务进入列表不可见。

### 7.2 What（用户裁决，2026-09-27）

| # | 议题 | 裁决 |
| :--- | :--- | :--- |
| D3 | `@` 语法 | **完全移除**：删除 `CaptureInputParser`、补全列表、补全键盘分支、小窗内新建项目能力（R-1.6/R-1.8 废弃） |
| D4 | 新建归属 | 当前下拉 `SelectedProject`（R-1.9） |
| D5 | 保存后 | **保持打开**，清空输入，新任务立即出现在列表；关闭靠 Esc/热键 |
| D6 | 键盘切换项目 | `Ctrl+Tab` 下一个、`Ctrl+Shift+Tab` 上一个，首尾循环 |
| D7 | 占位文字 | `Something to do...`（用户原话「不必过于直白」） |
| D8 | 底部提示 | 增加 `Ctrl+Tab 切换项目`；**[推断]** 文案「切换项目」，依据：与现有「保存」「关闭」同为动词短语 |

~~**[推断]** 保存后优先级复位为 P2：沿用现有 `ResetInput` 行为，未改动。~~
**已被用户推翻（2026-09-29）**：「enter 确认一个任务时优先级会重置到 p2，感觉保持不变比较好」。
回车保存后优先级保持，Esc 关闭才回到 P2。见 [spec-due-date-picker](../task-domain/spec-due-date-picker[DONE].md)。

### 7.3 Non-goals

- 主窗创建栏语法（保持原样）。
- 列表内键盘选中行 + Space 勾选（仍在 §5 Deferred）。

### 7.4 Change checklist

- [x] REQUIREMENTS：R-1.6/R-1.8 废弃，新增 R-1.9，推翻记录
- [x] spec-quick-window-hotkey-capture 头部废弃标注
- [x] `QuickCaptureViewModel`：删补全/解析/建项目；`SaveAsync` 改走 `AddTaskViewModel`（与主窗共用）归属 `SelectedProject`、不关窗；新增 `SelectNextProject`/`SelectPreviousProject`
- [x] `QuickCaptureWindow.axaml(.cs)`：删补全 UI 与分支；`Ctrl+Tab`；占位文字；底部提示
- [x] 删除 `CaptureInputParser.cs`、`CaptureInputParserTests.cs`
- [x] `QuickCaptureViewModelTests`：删 `@` 用例，新增归属/不关窗/循环切换用例

### 7.5 人工验收（owner 执行）

| # | 操作 | 应看到的现象 | 通过？ |
| :--- | :--- | :--- | :--- |
| L1 | 打开小窗 | 占位为 `Something to do...`；无任何 `@`/`#`/标签字样；底部有 `Ctrl+Tab 切换项目` | [ ] |
| L2 | 下拉选非 Default 项目 A，输入「买菜」回车 | 小窗**不关闭**，输入清空，「买菜」立即出现在 A 的列表；主窗 A 下也有 | [ ] |
| L3 | 连续输入两条并回车 | 两条都进入 A，可连续录入 | [ ] |
| L4 | 输入框聚焦时按 `Ctrl+Tab` / `Ctrl+Shift+Tab` | 下拉切到下一个/上一个项目并循环，列表同步刷新；焦点仍在输入框 | [ ] |
| L5 | 输入 `x @某项目` 回车 | 标题原样为「x @某项目」，归属当前下拉项目，不新建项目 | [ ] |
| L6 | Esc / 热键 | 小窗隐藏；再次打开恢复上次项目 | [ ] |

### 7.6 Progress log

#### 2026-09-27

- Completed：文档修订 + §7.4 全部代码项。`dotnet build` 0 警告 0 错误；`dotnet test` 269 通过
  （dev 276 − 解析器 9 − `@` 用例 1 + 新增 3；减少部分随 `@` 功能删除，非回归，AGENTS.md 272 基线需 owner 同步）。
- Decisions：D3–D8 见 §7.2。
- Owner 预览通过（「没问题」），SPEC 关闭为 `[DONE]`。
- Current resume point：无（已关闭）。
- Subagent/task references：无。
