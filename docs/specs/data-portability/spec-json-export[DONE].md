# spec-json-export: 设置 → 数据：导出全部项目与任务为 JSON

## Metadata

- **ID**: spec-json-export
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-10-01
- **Last Updated**: 2026-10-24

## Why

数据只存在 `%LOCALAPPDATA%\FlowTask\flowtask.db`，用户没有任何手段备份或带走数据。
所有者此前提过以后的同步走「云端或导入导出」；本轮裁决（2026-10-01）：「json，先导出吗，导入延缓」「新增数据」「导出目前只导出数据」。

## What

1. **入口**：设置新增分区「数据」（`SettingsSection.Data`），内含一个「导出为 JSON」按钮与一行说明。dev 已落地的左侧顺序是外观 → 通用 → 命令行 → 操作指南 → 关于。原先写「数据」放在「通用」之后，会占住「命令行」现在的位置。**已采纳（2026-10-24，所有者要求按提议执行）**：本 SPEC 只加入「数据」，位置在命令行之后、操作指南之前。当前顺序：外观 → 通用 → 命令行 → 数据 → 操作指南 → 关于。「已归档的项目」归 spec-project-archive，本分支不添加该分区；两份草稿共用的完整顺序仍是外观 → 通用 → 命令行 → 数据 → 已归档的项目 → 操作指南 → 关于。
2. **流程**：点击 → 系统保存对话框（Avalonia `StorageProvider.SaveFilePickerAsync`），默认文件名 `flowtask-export-YYYYMMDD.json`（本地日期，经 `IClock`）→ 写入 → 按钮下方显示「已导出 N 个项目、M 条任务」或失败原因。取消对话框不提示。
3. **内容**（UTF-8，缩进格式）：

   ```json
   {
     "schemaVersion": 1,
     "exportedAt": "2026-10-01T08:00:00Z",
     "projects": [
       { "id": "…", "name": "工作", "sortOrder": 1, "isArchived": false, "createdAt": "…Z" }
     ],
     "tasks": [
       { "id": "…", "projectId": "…", "title": "…", "priority": "High",
         "dueDate": "2026-10-03", "isCompleted": false, "completedAt": null,
         "isCliWritten": false, "isPinned": false, "pinnedAt": null, "createdAt": "…Z" }
     ]
   }
   ```

   - 项目含已归档项目（含 Default）。
   - 时刻字段为 UTC ISO-8601；`dueDate` 为日历日 `yyyy-MM-dd`，不做时区换算（design-domain-contract §3）。
   - `priority` 写枚举名而非整数，避免以后改枚举值时旧文件语义漂移。
   - 每条任务写入 `isCliWritten`（bool）。`TaskItem.IsCliWritten` 已在 dev 落地，是任务数据；任务行元信息处显示文字 `CLI`。导出这个布尔值，不另写界面上的 `CLI` 二字。该列已经在库里，不按「哪条分支先合入」决定有无。**已采纳（2026-10-24）**。
   - 示例里的 `isPinned` / `pinnedAt` 仍按 Constraints：`feature/task-pin` 先合入则带上，否则本分支先不含，由后合入者补。这两字段仍是条件项；`isCliWritten` 不是。
   - 2026-10-24 起，已完成任务在行上显示 `MM/dd~MM/dd 已完成`（创建日与完成日的本地月日）。这只是界面文案。导出仍写库里的 `dueDate` 与 `completedAt`，不写这句展示字符串。**已采纳（2026-10-24）**。
4. **不导出**：外观、关闭行为、默认到期、小窗记忆（所有者 2026-10-01：只导出数据）。命令行四项设置也留在 `AppSettings`：「接受命令行交互」「允许读取」「允许写入」「给命令行写入的任务加上 CLI 标记」。这四项按「只导出数据」排除。**已采纳（2026-10-24）**。另不导出：遗留列 `Description`、任务 `IsArchived` / `ArchivedAt` / `IsDeleted`、项目 `ColorHex`；`IsDeleted = 1` 的历史软删行；完成态展示文案 `MM/dd~MM/dd 已完成`。
5. **分层**：快照构建与序列化放 `FlowTask.Infrastructure`（`JsonDataExporter`，只依赖仓储接口并写入传入的 `Stream`）；Desktop 只负责对话框与提示。序列化用 .NET 自带 `System.Text.Json`，不引入新依赖。
6. **操作指南**（BR-1）：新增条目「导出数据」（设置组），场景 `ExportData` 演示点按钮 → 保存。
7. **备份文件与 CLI `list` 是两份产物**（spec-cli-add-delete 已合入 dev）。`list` 在 stdout 打印较短的 JSON 数组，覆盖每一条未删除任务（`IsDeleted = 0`，含已完成），字段只有 `id`、`title`、`project`（项目名）、`priority`、`due`、`completed`、`cli`。备份文件是设置里另存的完整数据快照：含已归档项目（含 Default）及其任务，字段见上面第 3 条。标记在文件里叫 `isCliWritten`，在 `list` 里叫 `cli`。**已采纳（2026-10-24）**。

## Non-goals

- 导入（延缓，所有者裁决）。`schemaVersion` 为以后导入识别格式预留。
- 导出设置项（含命令行那四项）。2026-10-24 已确认：已落地的命令行设置也算进这条既有排除。
- 把 CLI `list` 的 stdout JSON 当作备份文件。2026-10-24 已确认二者分列。
- 导出任务行上的 `MM/dd~MM/dd 已完成`，或任务行上的 `CLI` 二字。2026-10-24 已确认不导出。
- 自动定时备份、导出 CSV / Markdown。
- 加密或压缩。

## Constraints and decisions

- Article 6：导出字段清单只在导出 DTO 一处定义；`schemaVersion` 常量单点声明。
- Article 9：`exportedAt` 与默认文件名日期经 `IClock`。
- TR-1：`ExportDataViewModel` 放 `ViewModels/Actions/`。
- project-rules 架构规则：Infrastructure 不引用 Avalonia；Desktop 通过 `Stream` 传入目标。
- 若 `feature/task-pin` 先合入，`isPinned` / `pinnedAt` 一并导出；否则本分支不含这两字段，由后合入者补齐（实施时以 `dev` 现状为准）。`isCliWritten` 已在 dev 落地，不套用这条「先合入才导出」。
- 左侧顺序、备份文件与 `list` 的差别、完成态展示文案、命令行四项设置，见 What §1、§3、§4、§7。2026-10-24 所有者要求按文中提议执行，以上各项已采纳。
- 开工时本工作区从 `149e879` 快进到当时的 `dev`（`fa35f77`），不从 `149e879` 接着改。这次快进是所有者开工指示，不是修订草稿时的合并。
- **[推断]** 写入采用「先写同目录临时文件，成功后替换」。依据：用户覆盖已有导出文件时，写到一半失败不应毁掉旧备份。若 `IStorageFile` 无法取得本地路径（沙盒环境），退化为直接写流。

## Acceptance criteria

- [x] 设置左侧出现「数据」分区，顺序为外观 → 通用 → 命令行 → 数据 → 操作指南 → 关于；点「导出为 JSON」弹出系统保存对话框，默认文件名正确。对话框本身未做 headless 测试；默认文件名由视图模型单测覆盖。
- [x] 导出文件可被标准 JSON 解析；项目（含已归档、Default）与任务数量与库一致；任务含 `isCliWritten`；不含设置（含命令行四项）、完成态展示文案、排除字段与软删行。
- [x] 备份文件与 CLI `list` 的 stdout JSON 字段不同；文件仍含已归档项目及其任务。
- [x] `dueDate` 为 `yyyy-MM-dd`，时刻为 UTC `Z` 结尾。
- [x] 取消对话框无提示；写入失败显示原因且不留半截文件。
- [x] 操作指南新增条目与场景。
- [x] 所有者预览确认（2026-10-24：「没问题，合入dev」）。build 0 警告 / 0 错误，test 411 通过。

## Staged plan

实施起点：开工时已快进到 `dev`（`fa35f77`）。

1. Infrastructure：导出 DTO、`JsonDataExporter`；单测（字段、排除项、日期格式、软删过滤、`isCliWritten`）。
2. Desktop：`SettingsSection.Data`、`ExportDataViewModel`、保存对话框与结果提示。
3. 操作指南条目与场景。
4. build + test + 预览。

## Change checklist

- [x] `src/FlowTask.Infrastructure/Export/`（新：DTO、`JsonDataExporter`）
- [x] `src/FlowTask.Desktop/ViewModels/SettingsSection.cs`、`MainViewModel.cs`
- [x] `src/FlowTask.Desktop/ViewModels/Actions/ExportDataViewModel.cs`（新）
- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml(.cs)`（数据分区、保存对话框）
- [x] `src/FlowTask.Desktop/ViewModels/GuideCatalog.cs`、`Views/Guide/GuideScenes.cs`
- [x] `tests/`：导出器单测、VM 测试
- [x] `docs/rules/docs-conventions.md` 登记 `data-portability` area（本 SPEC 同一变更）
- [x] `docs/requirements/REQUIREMENTS.md` 增补导出需求条目
- [x] Guide: updated 导出数据

## Progress log

### 2026-10-01

- Completed: 所有者裁决 JSON、只导出、新增「数据」分区、只导出数据不含设置。
- Decisions: 新建 area `data-portability`（现有 area 均不拥有「数据进出应用」）。
- Current resume point: 等所有者确认本 SPEC → `[IN-PROGRESS]`。

### 2026-10-24

- Completed: 对照 dev 在 2026-10-01 之后已落地的设置顺序、`TaskItem.IsCliWritten`、CLI `list` 和完成态展示，修订本草稿供审核。未改代码，未提交。
- Decisions: 无新的所有者裁决。左侧顺序、导出 `isCliWritten`、备份文件与 `list` 分列、不导出完成态展示文案和命令行设置，都标为待本次审核。
- Current resume point: 等所有者审核本 SPEC。工作区仍在 `149e879`。
- Subagent/task references, when used: 无。

### 2026-10-24（开工）

- Completed: 所有者指示先把工作区快进到当时的 `dev`（`fa35f77`），再按本稿提议写代码，不要停下来问，不要提交，不要合入 dev。
- Decisions: 左侧只加「数据」（命令行之后、操作指南之前）；导出 `isCliWritten`；不导出完成态展示文案和命令行四项；备份文件与 `list` 分列。「已归档的项目」分区仍不在本 SPEC。
- Current resume point: build 0/0，test 411 通过，预览已发布。等所有者预览确认。
- Subagent/task references, when used: 无。

### 2026-10-24（预览通过）

- Completed: 所有者预览原话「没问题，合入dev」。SPEC 改为 `[DONE]`，与代码同一提交后合入 `dev`。不推远程。
- Decisions: 无新裁决。左侧顺序、`isCliWritten`、不导出完成态文案和命令行设置、备份文件与 `list` 分列，维持开工时采纳的提议。
- Current resume point: 合入 `dev`。
- Subagent/task references, when used: 无。

## Verification

- Automated: `dotnet build` 0 警告 / 0 错误；`dotnet test` 411 通过、0 失败。导出字段、排除项、与 `list` 的差别、取消与失败不留半截文件见 `JsonExportTests`。
- Manual: 所有者预览通过（2026-10-24：「没问题，合入dev」）。系统保存对话框未做 headless 测试。
- Not run or not covered: 系统保存对话框无法 headless 测试。

## Risks and open questions

- Owner: aisdwf
- 预览 exe 写的是 `flowtask.preview.db`，导出的也是预览库数据；验收时注意区分。
- 2026-10-24 已按提议采纳：左侧「数据」位于命令行与操作指南之间；任务字段 `isCliWritten`；命令行四项设置不进文件；备份文件与 CLI `list` 分列，且文件仍含已归档项目及其任务；不导出 `MM/dd~MM/dd 已完成`。

## Lessons learned

None yet.

## Related documents

- SPECs: [spec-task-pin](../task-domain/spec-task-pin[DRAFT].md)（置顶字段，仍按先合入与否决定是否导出）、spec-project-archive（「已归档的项目」分区；归档项目及其任务随导出）、[spec-cli-add-delete](../cli/spec-cli-add-delete[DONE].md)（dev 上已完成：`IsCliWritten`、任务行 `CLI`、stdout 的 `list`）
- ADRs: adr-technology-stack（本地优先）
- Rules: project-rules BR-1、technical-rules TR-1、AI_CONSTITUTION Article 6 / 9
- Analysis: design-domain-contract §3（时间语义）
