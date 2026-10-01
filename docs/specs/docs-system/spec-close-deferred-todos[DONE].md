# spec-close-deferred-todos: 关闭已裁决不做与已核销的 Deferred TODO

## Metadata

- **ID**: spec-close-deferred-todos
- **Type**: simple
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-10-01
- **Last Updated**: 2026-10-01

## Why

多份 `[DONE]` SPEC 的 Deferred 段仍挂着带期限的 `TODO(...)`，其中 `desc-field`、`recycle-bin` 期限 2026-10-05，
过期即违反 Article 4（「Pre-commit debt linter fails on expired … TODO」）。

2026-10-01 所有者逐项裁决：

- 今日聚焦：「关，并且今日这个也不做」（主窗与小窗均不做）。
- 备注：「todolist感觉保持简洁吧，更重的内容不该出现在这里，这里只是一个清单」。
- 删除撤销 / 回收站：「留五秒也没多少决策的空间，还不如把决策时间留给二级确认窗口。结合6归档，感觉更加没有意义了」。

另有几条 TODO 早已由后续 SPEC 落地，但原处从未划掉，读者会误以为仍是欠债。

## What

- 关闭为「不做」：`TODO(quick-capture-today)`、`TODO(today-view)`、`TODO(desc-field)`、`TODO(recycle-bin)`。
- 标为已核销：`TODO(appearance-persist)` / `TODO(persistence)` / `TODO(settings-store-unification)`（spec-appearance-persist）、`TODO(cleanup)`（`Class1.cs` 已不存在）。
- 三项「不做」写入 REQUIREMENTS §6 非目标，并在 §7 技术缺陷段注明已裁决。

## Non-goals

- 不改代码。`GetTodayTasksAsync` 及其测试保留不动：删它是独立的代码变更，需另行确认。
- 不物理删除 `Description` 列（design-domain-contract §2.4：sqlite-net 删列需重建表）。
- `TODO(archive-ui)`（期限 2026-10-12）不在此关闭，由后续 `feature/project-archive` 落地后核销。
- 不改写这些 SPEC 的其余历史叙述。

## Constraints and decisions

- Article 4：TODO 必须「解决或显式推迟」；所有者裁决「不做」即为解决，须留下理由与出处。
- Article 8：关闭处保留原条目并删除线标注，指向本 SPEC，不静默删除。
- rule-no-invented-user-behavior §2.2.5：产品前提的真源是 REQUIREMENTS，故「不做」须回写 §6。

## Acceptance criteria

- [x] `docs/` 内不再有未关闭且期限已到或将到的 `desc-field` / `recycle-bin` / `quick-capture-today` / `today-view` 条目。
- [x] 已落地的 `appearance-persist` / `persistence` / `settings-store-unification` / `cleanup` 在原处标为已核销。
- [x] REQUIREMENTS §6 含三项新非目标，引用所有者原话。
- [x] 所有者确认文档改动（2026-10-01「合入dev」）。

## Change checklist

- [x] `docs/specs/main-window/spec-classification-ui[DONE].md` §6
- [x] `docs/specs/main-window/spec-sidebar-selection-consolidation[DONE].md` §6
- [x] `docs/specs/task-domain/spec-task-contract-and-clock[DONE].md` §6
- [x] `docs/specs/task-domain/spec-due-date-calendar[DONE].md` §2.6 / §6 / §8
- [x] `docs/specs/task-domain/spec-due-date-picker[DONE].md` Non-goals
- [x] `docs/specs/quick-capture/spec-quick-window-single-project-list[DONE].md` §2.6 / §5
- [x] `docs/specs/visual-theme/spec-editorial-and-ripple-theme[DONE].md` §6
- [x] `docs/requirements/REQUIREMENTS.md` §6 / §7
- [x] Guide: not affected — 仅文档，无用户可见交互变化

## Progress log

### 2026-10-01

- Completed: 全库 grep `TODO(`，逐条核对现状（`Class1.cs` 不存在；外观已走 `AppSettings`），按上表改写。
- Decisions: 见 Why 中所有者原话；`archive-ui` 留给归档分支核销。
- 所有者确认「合入dev」：SPEC 改 `[DONE]`，引用同步更新，提交后经 `finish-task.ps1` 合入 `dev`。
- Current resume point: 已关闭。

## Verification

- Automated: `dotnet build` + `dotnet test`（`GuideMaintenanceTests` 会扫描本 SPEC 的 `Guide:` 行）。
- Automated result (2026-10-01): build 0 警告 0 错误；test 341 通过。
- Manual: 所有者审阅后确认合入（2026-10-01）。
- Not run or not covered: 无代码变更，无需预览验收。

## Risks and open questions

- Owner: aisdwf。`GetTodayTasksAsync` 已无生产调用方，是否删除留待所有者决定（不阻塞本 SPEC）。

## Lessons learned

后续 SPEC 落地前序 TODO 时，只在新 SPEC 里写「承接自…」，没有回到原处划掉，导致同一条 TODO 在 3–4 份文档里长期显示为未完成。核销应在原处完成。

## Related documents

- SPECs: spec-appearance-persist、spec-due-date-calendar、spec-task-contract-and-clock
- ADRs: none
- Rules: AI_CONSTITUTION Article 4 / 8、rule-no-invented-user-behavior
- Analysis: none
