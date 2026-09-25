# spec-remove-tag-feature: 移除标签功能

## Metadata

- **ID**: spec-remove-tag-feature
- **Type**: complex
- **Status**: in-progress
- **Owner**: aisdwf
- **Created Date**: 2026-09-24
- **Last Updated**: 2026-09-24

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-24）「一起做」，本 SPEC 与 `spec-create-task-inherits-selected-project` 同批开工。**

**上游依据**：
- 用户原话（本轮，2026-09-24）：「标签实际体验下来功能很累赘，可以考虑清理这个功能。」
- 用户裁决（本轮问答确认）：范围为**完全移除**（非仅精简 UI）；数据库中已存在的
  `Tags`/`TaskTags` 表**不做迁移**，保留但不再被代码引用；
  QuickCapture 小窗中 IN-PROGRESS 的 `#标签` 补全逻辑**一并删除**；
  确认推翻 `REQUIREMENTS.md` R-1.6/R-1.8 中对标签的既有裁决，本 SPEC 同步修订该文档。

> **⚠ 与既有裁决的冲突（必须显式记录）**：
> `spec-tag-entity[DONE]` 记录了标签功能是**用户此前明确裁决**的产物
> （原话：「我对标签的理解就是提供可删除的预设……后续直接选择标签，而不是手动输入」）。
> `REQUIREMENTS.md` R-1.6 / R-1.8 与 `design-interaction-principles.md` §4.2 /
> `design-domain-contract.md` §4.3 均将标签列为明确需求。
> 本 SPEC 的存在是用户在本轮对话中**推翻此前裁决**，不是撰写者的推断或建议。
> 依 `rule-no-invented-user-behavior.md` §2.1，此推翻已获用户本轮原话确认，可执行。

---

## 0. 新会话接手须知

1. 标签功能已完整实现，涉及 Core / Infrastructure / Desktop 三层 + 4 个测试文件，
   详见 §2.2 影响面清单（已通过 subagent 调研核实文件路径）。
2. 本 SPEC **直接影响另外两份 IN-PROGRESS SPEC**，须在同一改动中同步修订：
   - `spec-quick-window-hotkey-capture[IN-PROGRESS]`：已实现并机器验证通过的
     `#标签` 解析/补全/落库逐一移除，SPEC 正文与验收清单同步删除标签相关条目。
   - `spec-quick-window-single-project-list[IN-PROGRESS]`：调研确认未直接引用标签，
     实施时仍需二次确认无遗漏引用。
3. `docs/specs/README.md` 的待办事项索引中 `spec-tag-entity` 的
   `TODO(tag-filter)`（按标签筛选任务）随标签实体删除一并核销（不再适用）。
4. `spec-tag-entity[DONE]` 本身**不删除**，按 `docs-conventions.md` 终态要求
   改状态为 `[OBSOLETE]`，文件重命名，正文头部加废弃说明，指向本 SPEC。
5. 数据库迁移策略：**不写 DROP TABLE**。`Tags`/`TaskTags` 两张表在已有用户数据库中
   保留但成为死表；新安装的用户因代码不再调用 `CreateTableAsync<Tag>()` /
   `CreateTableAsync<TaskTag>()`，不会创建这两张表。此为用户本轮明确选择的方案
   （权衡：更安全，不做不可逆删除；代价：老库有垂悬表，可接受）。

---

## 1. Why

### 1.1 用户评价

用户本轮原话：「标签实际体验下来功能很累赘，可以考虑清理这个功能。」
即：功能已上线并实际使用过，经真实使用后判定其增加的操作负担大于收益。

### 1.2 与历史裁决的关系

`spec-tag-entity[DONE]` 是 2026-09-15 用户明确裁决后落地的功能，非本方臆造。
本次不是「发现旧设计有缺陷」，而是「用户基于实际使用体验，推翻此前的产品决策」。
两者性质不同，因此本 SPEC 不定性为 `Design Wrong` 或 `Code Wrong`（`spec-tag-entity`
的设计与实现都忠实还原了当时的用户裁决），而是产品方向的主动收缩。

### 1.3 Attribution

`Design Wrong`（追溯）—— 但根因不在实现或原设计的技术判断，而在于
「标签作为受管理预设集合」这一产品假设本身，经真实使用验证后被用户否定。
本条 Attribution 记录方向调整，不代表 `spec-tag-entity` 团队/流程存在过失。

---

## 2. What

### 2.1 范围裁决（本轮问答已确认，逐条记录）

| 议题 | 裁决 |
| :--- | :--- |
| 清理程度 | **完全移除**：Entity、Repository、ViewModel、UI、测试、数据库表创建逻辑全部删除，不保留"精简版" |
| 数据库现有数据 | **不迁移**：不写 DROP TABLE；`Tags`/`TaskTags` 表在老库中保留但不再被引用；新库不再创建 |
| QuickCapture 的 `#标签` 补全（IN-PROGRESS） | **一并删除**：连带修订 `spec-quick-window-hotkey-capture[IN-PROGRESS]` |
| `REQUIREMENTS.md` 中的标签条目 | **同步修订**：R-1.6 改为仅保留 `@项目`；R-1.8 同步移除标签部分；记录本次裁决变更 |

### 2.2 影响范围清单（已通过只读调研逐一核实）

**Core 层**（整体删除）：
- `src/FlowTask.Core/Models/Tag.cs`
- `src/FlowTask.Core/Models/TaskTag.cs`
- `src/FlowTask.Core/Models/TagName.cs`
- `src/FlowTask.Core/Models/TagCatalog.cs`
- `src/FlowTask.Core/Interfaces/ITagRepository.cs`

**Infrastructure 层**（整体删除）：
- `src/FlowTask.Infrastructure/Persistence/SqliteTagRepository.cs`
  （含 `CreateTableAsync<Tag>()` / `CreateTableAsync<TaskTag>()` 调用，删除后新库不再建表）

**Desktop 层 - ViewModel**（整体删除的文件）：
- `src/FlowTask.Desktop/ViewModels/Actions/CreateTagViewModel.cs`
- `src/FlowTask.Desktop/ViewModels/Actions/CommitRenameTagViewModel.cs`
- `src/FlowTask.Desktop/ViewModels/Actions/ChangeTagColorViewModel.cs`
- `src/FlowTask.Desktop/ViewModels/Actions/DeleteTagViewModel.cs`
- `src/FlowTask.Desktop/ViewModels/TagItemViewModel.cs`
- `src/FlowTask.Desktop/ViewModels/TagChoice.cs`

**Desktop 层 - ViewModel**（需摘除标签相关代码，文件本身保留）：
- `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`
  （`Tags` 投影、`NewTagChoices`、`NewTagName`、`CreateTagCommand` 等标签相关属性/命令）
- `src/FlowTask.Desktop/ViewModels/TaskRowViewModel.cs`
  （`AssignedTags` / `VisibleAssignedTags` / `HasTags` / `EditTagChoices` 等展示与编辑缓冲）
- `src/FlowTask.Desktop/ViewModels/Actions/AddTaskViewModel.cs`
  （`ReplaceTaskTagsAsync()` 调用与 `selectedTagIds` 参数）
- `src/FlowTask.Desktop/ViewModels/Actions/SaveEditTaskViewModel.cs`
  （标签关联保存逻辑）
- `src/FlowTask.Desktop/ViewModels/Actions/ToggleEditTaskViewModel.cs`
  （编辑缓冲中标签选择项初始化）
- `src/FlowTask.Desktop/ViewModels/QuickCaptureViewModel.cs`
  （`#标签` 补全逻辑，随本 SPEC 一并删除，见 §2.1）

**Desktop 层 - View**（`MainWindow.axaml` 需摘除的 4 处区块）：
- 创建区标签多选（约行 303-312）
- 任务行标签展示 `VisibleAssignedTags` / `+N` 折叠（约行 469-488）
- 编辑区标签多选（约行 550-557）
- 设置页「标签管理」区块（约行 758-837）

**Tests 层**（整体删除的文件）：
- `tests/FlowTask.Tests/SqliteTagRepositoryTests.cs`
- `tests/FlowTask.Tests/TagAndFactoryTests.cs`

**Tests 层**（需摘除标签相关用例，文件本身保留）：
- `tests/FlowTask.Tests/MainViewModelTests.cs`
- `tests/FlowTask.Tests/QuickCaptureViewModelTests.cs`

**跨 SPEC 文档修订**：
- `docs/specs/task-domain/spec-tag-entity[DONE].md` → 重命名为
  `spec-tag-entity[OBSOLETE].md`，正文头部加废弃说明（废弃原因、指向本 SPEC）
- `docs/specs/quick-capture/spec-quick-window-hotkey-capture[IN-PROGRESS].md`：
  移除 `#标签` 相关的需求引用（R-1.6 措辞）、解析规则（§2.3）、落库规则（§2.4）、
  已裁决表（D2）、验收清单中含标签的条目（H7/H8 等，改为仅验证 `@项目`）
- `docs/requirements/REQUIREMENTS.md`：
  - R-1.6 由「支持 `@项目` `#标签` 并补全」改为「支持 `@项目` 并补全」
  - R-1.8 移除「`#标签`」相关表述，仅保留 `@项目` 的未知 token 保存时创建规则
  - 在裁决记录处补充本次推翻的时间与依据（本轮用户原话）
- `docs/design/design-interaction-principles.md` §4.2「标签只选不输」：
  加废弃说明，指向本 SPEC
- `docs/design/design-domain-contract.md` §4.3「标签：受管理的预设集合」：
  加废弃说明，指向本 SPEC
- `docs/specs/README.md`：
  - SPEC 索引表更新 `spec-tag-entity` 状态为 `obsolete`
  - 待办事项索引核销 `TODO(tag-filter)`（不再适用，标签实体已删除）
  - 新增本 SPEC 的索引行

### 2.3 非目标

| 不做 | 理由 |
| :--- | :--- |
| 保留标签数据层供未来恢复 | 用户裁决为完全移除，非保留精简版 |
| 编写 DROP TABLE 迁移脚本 | 用户裁决不处理现有数据，风险最低方案 |
| 重新设计替代分类维度 | 未提出，超出本次范围；项目维度已存在，足够覆盖当前分类需求 |

---

## 3. 分阶段实施计划（须先呈现给用户确认）

- [x] **Phase 1: 生产代码删除/摘除**
  - [x] 删除 Core 层 5 个文件
  - [x] 删除 Infrastructure 层 1 个文件（含表创建逻辑）
  - [x] 删除 Desktop ViewModel 6 个独立文件（Actions 下 4 个 + TagItemViewModel + TagChoice）
  - [x] 摘除 `MainViewModel` / `TaskRowViewModel` / `AddTaskViewModel` /
        `SaveEditTaskViewModel` / `ToggleEditTaskViewModel` / `QuickCaptureViewModel`
        中的标签相关代码（`QuickCaptureViewModel` 额外改造：`CaptureInputParser` 同步
        改写为仅支持 `@项目`，去掉 `#标签` 解析分支）
  - [x] 摘除 `MainWindow.axaml` 4 处标签 UI 区块（创建区/任务行/编辑区/设置页），
        任务行 `Grid.ColumnDefinitions` 由 8 列收敛为 7 列
  - [x] 确认无遗留的 `using`/依赖注入注册引用已清理（`App.axaml.cs` 构造签名同步更新）
- [x] **Phase 2: 测试清理**
  - [x] 删除 `SqliteTagRepositoryTests.cs`、`TagAndFactoryTests.cs`
  - [x] 摘除 `MainViewModelTests.cs`（`SaveEdit_PersistsSelectedTags`）、
        `QuickCaptureViewModelTests.cs`（构造签名）中标签相关内容
  - [x] 额外发现并处理：`CaptureInputParserTests.cs` 因解析器签名变化整体改写；
        `SchemaMigrationTests.cs` 由「验证建表」改写为「验证不再建 Tag 表」（回归防护）
- [x] **Phase 3: 跨 SPEC 与需求文档修订**
  - [x] `spec-tag-entity[DONE]` → `[OBSOLETE]`（重命名 + 废弃说明）
  - [x] `spec-quick-window-hotkey-capture[IN-PROGRESS]` 摘除标签相关条目
        （历史记录保留，加 `[已随标签移除废弃]` 标注；验收清单去掉 H7，
        改写机器验证记录与前置条件）
  - [x] `REQUIREMENTS.md` R-1.6/R-1.8 修订 + §4.2 补充二次推翻记录
  - [x] `design-interaction-principles.md` §4.1/§4.2、`design-domain-contract.md`
        §2.2/§4.3 加废弃说明
  - [x] `docs/specs/README.md` 索引、待办事项（核销 `TODO(tag-filter)`）同步更新
- [x] **Phase 4: 验证**
  - [x] `dotnet build` 0 警告 0 错误 —— 2026-09-24 实测
  - [x] `dotnet test` 全绿 —— 2026-09-24 实测 180 通过（基线 197 - 17 个标签相关用例，非回归）
  - [x] 样式核验：`MainWindow.axaml` 删除区块后 `Grid.ColumnDefinitions` 已同步收敛，
        无孤立 `Grid.Column` 引用（编译器会暴露越界引用，已通过构建验证）

---

## 4. 执行记录与上下文追踪

### Subagent Log

| Timestamp | Subagent | Task | Task ID | Outcome |
| :--- | :--- | :--- | :--- | :--- |
| 2026-09-24 | explore | 摸底 Tag 功能全量文件清单与数据库路径 | ses_f2e9f8558ffeCf1e2pWG43kUjk | 产出完整文件清单，含 Core/Infra/Desktop/Tests/Docs 分类 |
| 2026-09-24 | explore | 确认 SQLite 数据库落盘路径与迁移机制现状 | ses_f2e8c60d5ffeT7M2T1sfSEvpok | 确认路径为 `~/Library/Application Support/FlowTask/flowtask.db`，无 DROP TABLE 机制 |

### 关键决策与状态增量

- **[2026-09-24]** 用户确认清理程度为完全移除，非仅精简 UI。
- **[2026-09-24]** 用户确认数据库不做迁移，`Tags`/`TaskTags` 表保留为死表。
- **[2026-09-24]** 用户确认 QuickCapture 的 `#标签` 补全一并删除。
- **[2026-09-24]** 用户确认推翻 `REQUIREMENTS.md` R-1.6/R-1.8 中的标签裁决，同步修订文档。
- **[2026-09-24]** 用户确认将 `spec-quick-window-hotkey-capture[IN-PROGRESS]` 一并修改，
  而非搁置或单独处理。
- **[2026-09-24]** Phase 1-4 机器侧全部完成：Core/Infrastructure/Desktop/Tests 四层
  标签相关代码与测试已删除或摘除；跨 SPEC（`spec-tag-entity` → `OBSOLETE`、
  `spec-quick-window-hotkey-capture` 摘除标签条目）与需求/设计文档
  （`REQUIREMENTS.md`、两份 design 文档）已同步修订；`docs/specs/README.md`
  索引与待办事项已更新。构建 0/0，测试 180 通过。等待用户人工验证后再转 `DONE`。
- **[2026-09-24]** 执行中发现范围外问题：`design-domain-contract.md` §2.3 早已规定
  `ProjectId` 不可为 null（应挂 Default 项目），但当前代码创建/删除路径仍写 `null`，
  与既定契约不一致。已就是否借本轮一并修复询问用户，用户回复「我不想做这个抉择，
  你自行决定」。评估后判定该修复涉及「全部任务」视图与 Default 项目信息架构合并，
  改动面超出本 SPEC 范围，**本轮不处理**，已登记至 `docs/specs/README.md`
  待办事项索引（关联 `spec-create-task-inherits-selected-project` §6）。

<!-- 以下按 Phase 实际完成情况追加，严禁提前填写 -->

---

## 5. 验证记录

### 机器门禁

- [x] 构建：`dotnet build FlowTask.sln` —— 0 警告 0 错误，2026-09-24 实测
- [x] 测试：`dotnet test` —— 180 通过，2026-09-24 实测

### 人工验证（由用户执行）

- [ ] 创建区、任务行、编辑区、设置页均无任何标签相关 UI 残留
- [ ] 快捷小窗 `#标签` 语法不再解析（作为普通文本进入标题）
- [ ] 现有数据库（若已有标签数据）应用启动正常，任务数据不受影响
- [ ] 新建数据库不再包含 `Tags`/`TaskTags` 表

---

## 6. Deferred Items

- 无（`spec-tag-entity` 的 `TODO(tag-filter)` 已核销，非推迟，见 §0.3）。

---

## 7. Commit Attribution 与经验教训

- **Attribution**: `Design Wrong`（追溯，非过失）—— 「标签作为受管理预设集合」
  的产品假设经真实使用验证后被用户否定，属产品方向调整。

---

## 8. Lessons Learned

<!-- 执行过程中如实追加，严禁预先编写 -->
