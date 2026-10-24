# spec-cli-add-delete: CLI 加入与删除任务

## Metadata

- **ID**: spec-cli-add-delete
- **Type**: complex
- **Status**: done
- **Owner**: owner
- **Created Date**: 2026-10-24
- **Last Updated**: 2026-10-24

---

## Why

外部程序需要往 FlowTask 里加入任务、删除任务。应用不负责调度这些程序。

所有者原话（2026-10-24）：「只要有加入和删除不就行了？外置的定时任务怎么调用 cli 是外面的问题。只要提供能加入和删除的功能就行。」「需要配置权限，是否接受 cli 交互以及 cli 写入的内容是否添加特殊标签，以免 agent 误操作之类的内容。」「cli 操作以后，就应该实时的刷新各个位置的内容，同步时非常重要的。」「cli 交互还需要分开读和写权限，总开关打开以后可配置。」

同一天的表单选定：标题必填，项目、优先级、到期日可选；删除只按任务 id；标记只做显示、放在任务行上；新的设置分区；总开关默认关、标记默认开；写入后通知正在运行的桌面程序。随后确认其余内容并要求开工。

进程内 `TaskChangeBus` 看不见另一个进程。设置里原先没有这些开关。

## What

短生命周期命令行，与桌面共用 `DatabaseLocation` 的同一个 SQLite 文件。

命令：

- `list`：读权限打开时，把未删除任务以 JSON 打到 stdout（id、标题、项目名、优先级、到期日、完成、是否带标记）。
- `add --title`：写权限打开时加入一条。可选 `--project`（项目名）、`--priority`（Low / Medium / High）、`--due`（yyyy-MM-dd）。
- `delete --id`：写权限打开时按 id 物理删除，不看标记。

权限在 `AppSettings`，由 `CliAccessPolicy` 统一解析：

1. 总开关「接受命令行交互」，默认关。关闭时读和写都不执行。
2. 允许读取、允许写入，默认都关。只在总开关打开后可以配置，也只在总开关打开后生效。
3. 给之后的命令行写入加上 `CLI` 标记，默认开。关掉不清除已有标记。

标记是 `TaskItem.IsCliWritten`。不恢复标签，不用 `Description`。

写入成功后，命令行向正在运行的桌面发送一条本机通知（`CliChangeChannel`，不复用 `FlowTask.SingleInstance.Pipe`）。主窗口重载任务与侧边栏计数，快捷小窗重载当前项目列表。桌面没开时通知落空，数据仍在库里。

拒绝时进程以非 0 退出，原因在 stderr，stdout 不打任务内容。项目名不存在则拒绝，不自动建项目。省略项目则落入 Default。

## Non-goals

- 应用内定时任务、来源抓取、来源键、幂等去重。
- 完成、改标题、改优先级、改到期日、改项目、建项目、归档、删项目。
- 用标记限制删除对象。
- 外观、快捷键、托盘策略。

## Constraints and decisions

- 命令行项目只依赖 Core 与 Infrastructure。桌面仍是单进程多窗口。
- 加入走 `TaskItemFactory.Create` 与 `SaveTaskAsync`。空白标题拒绝。`CreatedAt` 经 `IClock`。到期日由仓储归一化为本地日历日。
- 删除走 `PermanentDeleteAsync`。id 不存在或只剩历史软删行时不改库。
- 读、写默认关，避免总开关一打开就同时放行。这是随「总开关默认关」一起落地的取值，预览时可以改。
- 分区名「命令行」，顺序为外观、通用、命令行、操作指南、关于。短标记文案 `CLI`。
- 刷新复用现有列表装载。通知失败不回滚已写入的任务。

## Acceptance criteria

- [x] 总开关关闭时，`list` / `add` / `delete` 都不改变数据库，也不输出任务内容。
- [x] 总开关打开但读关闭时，`list` 被拒绝；写关闭时，`add` 和 `delete` 被拒绝。
- [x] 读打开时，`list` 的 JSON 含未删除任务的 id。
- [x] 写打开时，`add` 写入标题；省略项为 Default、中优先级、无到期日。给出的项目名、优先级、到期日写入对应字段。标记开关打开时 `IsCliWritten` 为 true；关闭时本次写入为 false。任务行绑定 `ShowCliMark`。
- [x] 项目名不存在、标题空白、id 不存在时，数据库不变。
- [x] 写打开时，`delete --id` 删除该任务，不论有没有标记。界面经工厂创建的任务没有标记。
- [x] 桌面开着且与命令行使用同一个库时，写入后列表能看到结果。所有者 2026-10-24 确认初步功能正确。
- [x] 设置里有「命令行」分区。所有者在预览中打开过总开关、读取和写入。
- [x] 操作指南写明该分区、两项权限和任务行标记。

## Staged plan

1. 把原话写入 REQUIREMENTS，本文件改为进行中。
2. `CliAccessPolicy` 与设置分区。
3. `IsCliWritten` 与任务行标记。
4. 控制台的 `list` / `add` / `delete`。
5. 本机通知与两个窗口的重载。
6. 测试、操作指南、构建与预览。提交等所有者手动验证。

## Change checklist

- [x] `docs/requirements/REQUIREMENTS.md`
- [x] `docs/rules/docs-conventions.md`：登记 `docs/specs/cli/`。
- [x] `docs/design/design-domain-contract.md`：`IsCliWritten`。
- [x] `FlowTask.sln` 与 `FlowTask.Cli`。
- [x] `CliAccessPolicy`、设置分区、`AppSettings` 键。
- [x] `TaskItem.IsCliWritten` 与主窗、小窗任务行标记。
- [x] `CliChangeChannel` 与桌面监听；不改单实例退出管道。
- [x] `GuideCatalog.cs` / `GuideScenes.cs`。
- [x] Guide: updated 命令行写入

## Progress log

### 2026-10-24

- Completed: 工作区 `feature/cli-add-delete` 自 `dev` @ `87fb535` 建立。草稿写完。
- Decisions: 范围限于加入、删除、权限、标记、开窗刷新。
- Current resume point: 见下一节。
- Subagent/task references, when used: 无。

### 2026-10-24 确认开工

- Completed: 所有者确认表单选择，并追加「分开读和写权限，总开关打开以后可配置」，然后要求开始。
- Decisions: 读权限对应 `list`。读、写默认关，仅总开关打开后可配置并生效。标记默认开。其余按已确认的 SPEC。
- Current resume point: 见下一节。
- Subagent/task references, when used: 无。

### 2026-10-24 所有者接受

- Completed: 所有者用预览和命令行试过加入、列出、删除和设置开关。
- Decisions: 当前独立的 `FlowTask.Cli` 符合预期，合入 `dev`。之后曾说明希望命令进 `FlowTask.exe`，那不是本次已接受的范围。
- Current resume point: SPEC 关闭为 `[DONE]`。
- Subagent/task references, when used: 无。

## Verification

- Automated: `dotnet build FlowTask.sln -v q --nologo` 0 警告 / 0 错误。`dotnet test FlowTask.sln --nologo -v q` 404 通过、0 失败。
- Manual: 所有者 2026-10-24 原话「目前初步功能是正确的」「目前的修改符合我的预期」。已试过设置开关、`add`、`list`、`delete`。
- Not run or not covered: 把命令收进同一个 `FlowTask.exe`。优先级非法字符串有代码分支，测试未单列。

## Risks and open questions

- 接受写入后，知道 id 的调用方可以删除界面里自己建的任务。标记只帮助分辨。
- 桌面没运行时没有刷新对象。
- 命令行与桌面同时打开同一个库。锁冲突时命令失败退出，不另写一个库。
- **Owner:** 所有者。
- **Blocker or trigger:** 无。预览时若要改读/写的默认关，记在这里再改。

## Lessons learned

`list` 把中文写成 `\u` 转义。机器解析后仍是原文；英文按原文显示。同名任务是数据里的两条记录，不是输出不确定。

命令行目前是单独的 `FlowTask.Cli`。所有者随后希望它进 `FlowTask.exe`，以免再装一个程序。那一步不在本次合入里。

## Related documents

- SPECs: [spec-remove-tag-feature](../task-domain/spec-remove-tag-feature[DONE].md)，[spec-onboarding-guide](../main-window/spec-onboarding-guide[DONE].md)，[spec-close-to-tray](../main-window/spec-close-to-tray[DONE].md)
- ADRs: [adr-technology-stack](../../adr/adr-technology-stack.md)
- Rules: [project-rules.md](../../rules/project-rules.md)（BR-1），[docs-conventions.md](../../rules/docs-conventions.md)
- Analysis: None
