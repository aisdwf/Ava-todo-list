# spec-appearance-persist: 外观偏好跨启动持久化

## Metadata

- **ID**: spec-appearance-persist
- **Type**: simple
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-26
- **Last Updated**: 2026-09-26

---

## Why

关闭再打开 exe 时总是默认深色 + 第一套命名主题 + Mica。用户在设置里选过的主题、材质和昼夜不会回来。

根因是实现缺口，不是存储缺失：`IAppSettingsRepository` / SQLite `AppSettings` 已存在，到期偏移和小窗上次项目已经走同一张表。外观三项只活在 `MainViewModel` 字段默认值里，`InitializeAsync` 只把这套默认再刷到界面。此项原登记为 `spec-editorial-and-ripple-theme` 的 `TODO(persistence)`（期限 2026-09-28）。

## What

把命名主题 id、材质 id、昼夜写入既有 `AppSettings` 键值表；启动时先读再应用到窗口。强调色选择器已从设置页撤下，不单独存（主题预设自带强调色）。

键名（Desktop 层权威，不泄漏进 Core）：

- `Appearance.ThemePresetId`
- `Appearance.MaterialId`
- `Appearance.IsDark`（`1` / `0`）

未知或损坏的 id 回退到各列表第一项（与现有 `FindThemePreset` / `FindMaterial` 一致）。缺键时保持编译期默认：深色、`default` 主题、Mica。

## Non-goals

- 不记忆窗口位置、尺寸、设置页停留在哪一栏。
- 不新增第二套配置文件或独立表。
- 不恢复已撤下的独立强调色选择器。

## Constraints and decisions

- 所有者 2026-09-26 确认本任务 restatement 与计划后开工。
- 存储走同一 `AppSettings`（`spec-due-date-calendar` 已定，避免双轨，Article 6）。
- 键名留在 `AppearanceCoordinator`：外观是 UI 概念，不把主题 id 写进 Core 仓储接口。
- 启动时必须在应用窗口主题/材质之前读库，避免先闪默认再跳到用户选择。
- 所有者 2026-09-26 原话「没问题」通过预览，要求合并。

## Acceptance criteria

- [x] 选一个非默认命名主题、非 Mica 材质、切到浅色，关掉 exe 再打开，三项均恢复。所有者 2026-09-26 原话「没问题」。
- [x] 库中未知主题/材质 id 时回退到列表第一项，不崩溃。
- [x] 从未写过外观键时仍为深色 + 默认主题 + Mica。
- [x] `dotnet build` 0 警告 0 错误；`dotnet test` 不回退。2026-09-26：190 通过 / 0 失败。

## Change checklist

- [x] `AppearanceCoordinator`：键名常量 + `ParseIsDark`
- [x] `MainViewModel`：启动加载、变更写入
- [x] `MainWindow.Opened`：先 `LoadAppearanceAsync` 再 `ApplyTheme` / `ApplyMaterial`
- [x] 测试：round-trip、未知 id 回退、缺键默认
- [x] 本 SPEC + `docs/specs/README.md` 索引

## Progress log

### 2026-09-26

- Completed: 所有者确认 restatement；worktree `bugfix/appearance-persist` 从本地 `dev` 创建。加载/保存与测试已落地。`dotnet build` 0 警告；`dotnet test` 190 通过。所有者预览原话「没问题」，SPEC 关闭为 `[DONE]`。
- Decisions: 三项键；强调色不单存；未知 id 回退第一项；启动先读库再刷窗。
- Current resume point: 本 SPEC 关闭。下一步合入 `dev`。

## Verification

- Automated: `dotnet build FlowTask.sln` 0 警告 0 错误；`dotnet test FlowTask.sln` 190 通过 / 0 失败（2026-09-26）。
- Manual: 所有者 2026-09-26 用 `preview/bugfix/appearance-persist/FlowTask.exe` 验收，原话「没问题」。
- Not run or not covered: 窗口几何记忆（非目标）

## Risks and open questions

- Owner: 无待裁决项。
- Blocker or trigger: 无。

## Lessons learned

- 已有键值表却只给部分设置接上读写，会造成「有的设置会记、有的不会」的假象。新的外观键必须在启动路径上先读再刷窗，不能等任务列表加载完。
- 属性变更回调不能是 async：需要可等待的落盘任务，否则测试无法证明 round-trip，进程立刻退出也可能丢掉最后一次点击。

## Related documents

- SPECs: [spec-editorial-and-ripple-theme[DONE]](./spec-editorial-and-ripple-theme[DONE].md)（原推迟项）；[spec-due-date-calendar[DONE]](../task-domain/spec-due-date-calendar[DONE].md)（AppSettings 单轨）；[spec-settings-master-detail-and-theme-presets[DONE]](./spec-settings-master-detail-and-theme-presets[DONE].md)（主题/材质 UI）
- Rules: `AI_CONSTITUTION.md` Article 2 / 6；`docs/rules/workflow-methodology.md`
