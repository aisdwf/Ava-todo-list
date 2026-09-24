# spec-tag-entity: 标签实体化与设置页管理

## Metadata

- **ID**: spec-tag-entity
- **Type**: complex
- **Status**: obsolete
- **Owner**: aisdwf
- **Created Date**: 2026-09-15
- **Last Updated**: 2026-09-24

> ## ⚠ 作废说明（docs-conventions §终态要求）
>
> **本 SPEC 记录的标签功能已被 [spec-remove-tag-feature](./spec-remove-tag-feature[IN-PROGRESS].md) 完全移除。**
>
> **作废原因**：标签功能本身按用户当时的明确裁决（见下方 §1.1 原话）正确落地并交付，
> 机器验证与人工验证均已在 2026-09-15 通过 —— 本 SPEC 记录的实现**没有过失**。
> 2026-09-24，用户基于**实际使用体验**重新评估，认为标签功能「很累赘」，
> 主动裁决完全移除。这是产品方向的调整，不是本 SPEC 设计或实现的缺陷。
>
> **是否有部分保留**：无。Core/Infrastructure/ViewModel/UI/测试/内置预设标签全部移除；
> 数据库中已存在的 `Tags`/`TaskTags` 表按用户裁决不做迁移，保留为死表，不再被代码引用。
>
> **当前应参照**：[spec-remove-tag-feature](./spec-remove-tag-feature[IN-PROGRESS].md)。
> 本文件自本次作废起仅作历史留存，不可再作为实现依据。
>
> ---
>
> **本 SPEC 已获用户显式确认并进入 `in-progress`。**
> 本轮按用户决定直接切换到最新标签实体模型，**不兼容现有逗号分隔标签数据**，
> 不实现旧字段迁移、回退读取或双写路径。

**上游依据**：
- 需求：[REQUIREMENTS](../../requirements/REQUIREMENTS.md) R-2.1 / R-2.3 / R-2.5
- 设计：[design-interaction-principles](../../design/design-interaction-principles.md) §4（标签方案 **B 已由用户裁决**）

**建议在 [spec-sidebar-selection-consolidation](../main-window/spec-sidebar-selection-consolidation[DONE].md) 之后执行** ——
侧边栏选中机制已完成收敛，本 SPEC 在现有设置视图中增加标签管理内容。

---

## 0. 新会话接手须知

与 [spec-sidebar-selection-consolidation §0](../main-window/spec-sidebar-selection-consolidation[DONE].md) 相同，
额外注意本 SPEC 的**迁移影响面**（见 §2.4，已逐一核实位置）。

---

## 1. Why

### 1.1 用户裁决与原话

> 「b，我对标签的理解就是提供可删除的预设，并且支持自己定义一些（设置页面）。
> 后续直接选择标签，而不是手动输入。todolist 不需要给自己加 note，
> 这些配置严禁出现强制依赖手动输入的内容。」

拆解为四条要求：

| 用户表述 | 设计含义 |
| :--- | :--- |
| 「方案 b」 | 标签升级为**独立实体表** |
| 「提供可删除的预设」 | 内置一批开箱可用标签，用户可删 |
| 「支持自己定义一些（设置页面）」 | 新建标签入口在**设置页**，不在任务流程中 |
| 「直接选择标签，而不是手动输入」 | 任务侧**只能选**，不提供自由文本输入 |

### 1.2 现状缺陷

`TaskItem.Tags` 为逗号分隔字符串（`TaskItem.cs:69`），
编辑态提供纯文本框让用户手输（`MainWindow.axaml:449`）。

用户评价：「**手动输入的标签对于后续的维护是灾难级的**」。

design-domain-contract §4.3 选择字符串存储时，**明示接受的代价正是「无法重命名标签」**。
现在需要重命名与删除治理能力，该代价不再可接受。

### 1.3 为什么必须改存储而非仅改 UI

若保留字符串存储只改录入方式（改为从已有值中选），
重命名仍需全表扫描逐条字符串替换 —— 非原子操作，
中途失败会留下部分改名的脏数据。而标签一旦成为**受管理的预设集合**，
重命名就是必备操作而非边缘需求。

**判据一致性**：design-domain-contract §4.2 正是以「需要重命名故值得独立实体」
判定项目该建表。同样判据应得同样结论，否则是双标。

**Attribution**: `Design Wrong` —— design-domain-contract §4.3 低估了标签的治理需求，
把它定位为「轻量随手输入」而非「受管理集合」。

---

## 2. What

### 2.1 数据契约变更

**新增 `Tag` 表**：

| 字段 | 类型 | 说明 |
| :--- | :--- | :--- |
| `Id` | `string` | 主键，GUID |
| `Name` | `string` | 标签名，唯一（大小写不敏感） |
| `ColorHex` | `string` | 标签色 |
| `SortOrder` | `int` | 展示顺序 |
| `IsBuiltIn` | `bool` | 是否为内置预设（**[推断]** 用于区分能否删除，见 §2.5） |
| `CreatedAt` | `DateTime` | 创建时刻（UTC，经 `IClock`） |

**新增 `TaskTag` 关联表**（多对多）：

| 字段 | 类型 |
| :--- | :--- |
| `TaskId` | `string` |
| `TagId` | `string` |

> **关键技术约束（已实测确认）**：`sqlite-net-pcl` 1.9.172
> **无关系映射能力** —— 无 `OneToMany` / `ManyToMany` / `GetChildren`，
> 全库仅一个 `CollationAttribute`。
> 因此**全部关联查询与组装必须手写**，不存在导航属性。

**`TaskItem.Tags` 字段处置** —— 删除字段及其所有读写路径。
本轮不兼容旧逗号分隔标签数据；新数据库只使用 `Tag` 与 `TaskTag`
实体关联模型。旧数据库中的字符串标签不属于本轮保留目标。

### 2.2 内置预设标签

内置目录：`紧急` `重要` `日常` `学习` `工作` `个人`

**推断依据**：需覆盖通用待办场景，且数量克制（用户明示「需要配置的不多」）。
**此项需评审确认**，包括是否要内置、以及具体词条。

内置标签**可删除**（用户明示「提供**可删除**的预设），
`IsBuiltIn` 仅用于标识来源，不作为删除保护。

### 2.3 交互变更

| 位置 | 变更 |
| :--- | :--- |
| 任务编辑态标签输入框 | **移除**自由文本框，改为多选选择器 |
| 顶部创建区 | 增设标签多选（属 design-interaction-principles 的「配置常驻」原则，**若该段先行则本段只需对接**） |
| 设置页 | 新增「标签管理」项 |

**标签管理能力**：列出全部标签及使用计数、新建、重命名、改色、删除。
标签合并不纳入本轮实现。

### 2.4 旧字符串模型移除影响面（已逐一核实）

旧模型引用的位置，必须全部移除：

**生产代码**：
- `src/FlowTask.Core/Models/TaskItem.cs`（旧字段定义）
- `src/FlowTask.Core/Models/TagNormalizer.cs`（旧规范化类）
- `src/FlowTask.Core/Models/TaskItemFactory.cs`（旧标签参数）
- `src/FlowTask.Core/Interfaces/ITaskRepository.cs`（旧标签查询方法）
- `src/FlowTask.Infrastructure/Persistence/SqliteTaskRepository.cs`（旧标签读写）
- `src/FlowTask.Desktop/ViewModels/TaskRowViewModel.cs`（旧文本编辑缓冲）
- `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`（旧字符串保存路径）
- `src/FlowTask.Desktop/Converters/DueDateConverters.cs`（旧标签转换器）
- `src/FlowTask.Desktop/Views/MainWindow.axaml`（旧文本输入与转换器绑定）

**测试**（预期需改写）：
- `tests/FlowTask.Tests/TagAndFactoryTests.cs`（改为 `TagName` 契约测试）
- `tests/FlowTask.Tests/SchemaMigrationTests.cs`（改为新表结构测试）
- `tests/FlowTask.Tests/ValidationTests.cs`（移除旧字符串标签兜底测试）
- `tests/FlowTask.Tests/MainViewModelTests.cs`（改为实体标签选择测试）

> **`TagNormalizer` 的去向**：删除。标签名称规范化由 `TagName` 作为唯一真源。

### 2.5 非目标

| 不做 | 理由 |
| :--- | :--- |
| 侧边栏信息架构重构 | 侧边栏信息架构（design-interaction-principles §7.2，方案待裁决） 待裁决 |
| 顶部常驻配置区的其他字段（项目/日期） | 独立 SPEC |
| 按标签筛选任务 | 未纳入 REQUIREMENTS，需先确认 |
| 标签层级 / 标签组 | 超出「轻量」定位 |
| 启用 `Description` | **已决定废弃**（用户明示不需要 note） |

---

## 3. 分阶段实施计划（须先呈现给用户确认）

- [x] **Phase 1: 实体与仓储**
  - [x] 新增 `Tag` / `TaskTag` 模型与 `ITagRepository`
  - [x] 新增 `TagName` 值对象
  - [x] `SqliteTagRepository`：CRUD、重命名、删除（事务清理关联）
  - [x] 补测试：唯一性约束、删除时关联清理
- [x] **Phase 2: 新库初始化**
  - [x] 标签表与关联表在新数据库初始化
  - [x] 内置预设标签的首次注入
  - [x] 初始化重复执行不产生重复标签
- [x] **Phase 3: 任务侧读写改造**
  - [x] `ITaskRepository` 移除旧字符串标签方法
  - [x] `TaskRowViewModel` 标签展示改为实体列表
  - [x] 移除 `TagsToListConverter`
- [x] **Phase 4: 选择器与设置页管理**
  - [x] 标签多选控件（**严禁**保留自由文本输入）
  - [x] 设置页「标签管理」：列表 + 计数 + 新建/重命名/改色/删除
  - [x] 任务行标签显示上限与 `+N` 折叠
- [x] **Phase 5: 验证**
  - [x] 全量构建 + 测试绿灯
  - [x] 样式类与令牌键存在性核验（防 Avalonia 静默失败）
  - [x] 在真实数据库上验证新标签模型初始化
  - [x] 文档同步

---

## 4. 执行记录与上下文追踪

### Subagent Log

| Timestamp | Subagent | Task | Task ID | Outcome |
| :--- | :--- | :--- | :--- | :--- |
| — | — | 暂未派发 | — | — |

### 关键决策与状态增量

- **[2026-09-15]** 用户明确授权开始执行；SPEC 状态由 `draft` 改为 `in-progress`。
- **[2026-09-15]** 用户决定不兼容现有逗号分隔标签数据，直接切换为实体关联模型；
  不实现旧字段迁移、回退读取或双写路径。
- **[2026-09-15]** 标签方案 B 由用户明确裁决，非撰写者推断。
- **[2026-09-15]** 已核实 `sqlite-net-pcl` 无关系映射能力，
  关联查询须全部手写（此结论来自 design-domain-contract §1.1 的实测记录）。
- **[2026-09-15]** 已核实迁移影响面：12 处生产代码 + 5 个测试文件（见 §2.4）。
- **[2026-09-15]** Phase 2～4 收尾完成：`SqliteTagRepository.InitializeAsync` 建表并幂等注入内置标签；任务行 `VisibleTagLimit=2` + `+N` 折叠布局修正为同列横向排列；设置页标签管理与任务侧多选已接通。

<!-- 以下按 Phase 实际完成情况追加，严禁提前填写 -->

### Phase 执行记录

- **[2026-09-15] Phase 2**：`CreateTableAsync<Tag/TaskTag>` + `EnsureBuiltInsAsync`；测试 `Initialize_SeedsBuiltInsIdempotently` 覆盖重复初始化。
- **[2026-09-15] Phase 4 收尾**：`MainWindow.axaml` 将可见标签与 `+N` 收入同一 `StackPanel`，避免同 Grid 列重叠。
- **[2026-09-15] Phase 5（机器侧）**：`dotnet build` 0/0；`dotnet test` 158 通过；样式核验确认 `TagChip` / `SettingsCard` / `SectionTitle` / `HexToBrushConverter` 存在。

---

## 5. 验证记录

### 机器门禁

- [x] 构建：`dotnet build FlowTask.sln`（0 警告 0 错误）—— 2026-09-15 实测
- [x] 测试：`dotnet test` —— 2026-09-15 实测 **158 通过**（旧字符串标签用例已移除/改写，总数相对原基线 163 下降属预期）
- [x] 初始化幂等性：同一新库重复初始化，内置标签数不增长（`Initialize_SeedsBuiltInsIdempotently`）

### 人工验证（由用户执行）

- [x] 首次启动 → 内置预设标签已出现在设置页 —— 2026-09-15 用户确认通过
- [x] 新数据库初始化后内置标签正确显示 —— 2026-09-15 用户确认通过
- [x] 设置页新建标签 → 任务侧选择器立即可选 —— 2026-09-15 用户确认通过
- [x] 重命名标签 → 所有引用该标签的任务同步更新 —— 2026-09-15 用户确认通过
- [x] 删除标签 → 从所有任务移除，任务本身不受影响 —— 2026-09-15 用户确认通过
- [x] 任务编辑 → **确认已无任何自由文本标签输入框** —— 2026-09-15 用户确认通过
- [x] 任务行标签超过上限 → 折叠为 `+N` —— 2026-09-15 用户确认通过

---

## 6. Deferred Items

同 [spec-sidebar-selection-consolidation §6](../main-window/spec-sidebar-selection-consolidation[DONE].md)，另加：

- **TODO(tag-filter)**: 按标签筛选任务尚未纳入需求，需先确认是否需要。

---

## 7. Commit Attribution 与经验教训

- **Attribution**: `Design Wrong` —— design-domain-contract §4.3 把标签定位为
  「轻量随手输入」，低估了其治理需求；且当时明示接受的
  「无法重命名」代价在真实使用中不可接受。
- **Lessons Learned**: 见 §8。

---

## 8. Lessons Learned

<!-- 执行过程中如实追加，严禁预先编写 -->
