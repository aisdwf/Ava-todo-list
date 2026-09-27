# spec-startup-theme-no-flash: 启动时直接呈现已保存的风格预设

## Metadata

- **ID**: spec-startup-theme-no-flash
- **Type**: simple
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-27
- **Last Updated**: 2026-09-27

## Why

命名主题、材质、昼夜已经会写入 `AppSettings` 并在下次启动恢复。但用户仍会先看到编译期默认风格（深色 + `default` 主题），再明显跳到上次选的预设。

根因是挂点错了，不是没读库：`LoadAppearanceAsync` 走主窗 `Opened` → `InitializeAsync`，窗口此时已经按 `App.axaml` 的 `RequestedThemeVariant="Dark"` 和字段默认值画完第一帧。`spec-appearance-persist` 写过「先读库再刷窗、不要等任务列表」，实现却把读库放在 `Show` 之后。

## What

在构造并 `Show` 主窗之前调用 `LoadAppearanceAsync`，把主题预设和昼夜写进应用级主题字典。主窗构造时立刻按已恢复的材质档位套背景。`Opened` 只继续加载任务等数据；若启动路径已经恢复过外观，不再在首帧之后重刷。

## Non-goals

- 不改预设色值、设置页交互、持久化键名。
- 不记忆窗口位置与尺寸。

## Constraints and decisions

- 所有者 2026-09-27 确认 restatement 后开工。
- `ShutdownMode.OnExplicitShutdown` 已存在，允许在设置 `MainWindow` 之前 `await` 读库。
- 键名与回退规则仍以 `AppearanceCoordinator` / `spec-appearance-persist` 为准。
- 启动读库失败时仍显示窗口（编译期默认），`Opened` 里的 `InitializeAsync` 再试一次。
- 所有者 2026-09-27 原话「没问题」，要求合并到 `dev`。

## Acceptance criteria

- [x] 选非默认命名主题（及非默认昼夜/材质）后关掉再打开，第一眼就是该预设，没有默认风格先闪再切。所有者 2026-09-27 原话「没问题」。
- [x] 从未写过外观键时仍为深色 + 默认主题 + Mica。
- [x] `dotnet build` 0 警告 0 错误；`dotnet test` 不回退。

## Change checklist

- [x] `App.axaml.cs`：`LoadAppearanceAsync` 后再 `new MainWindow` / `Show`
- [x] `MainWindow` 构造时 `ApplyMaterial`；`Opened` 不再等任务加载完才套外观
- [x] `MainViewModel`：启动已恢复外观则 `InitializeAsync` 不重刷
- [x] 测试锁住「读库早于构造主窗」
- [x] 本 SPEC + `docs/specs/README.md` 索引

## Progress log

### 2026-09-27

- Completed: 所有者确认 restatement；worktree `bugfix/startup-theme-no-flash` 从本地 `dev` 创建。启动路径改为 Show 前读库；构造时套材质；已加载则 Opened 不重刷。`dotnet build` 0 警告；`dotnet test` 273 通过。所有者预览原话「没问题」，SPEC 关闭为 `[DONE]`，合入 `dev`。
- Decisions: 显示前读库；构造时套材质；已加载则 Opened 不重刷。
- Current resume point: 本 SPEC 关闭。下一步合入 `dev`。

## Verification

- Automated: `dotnet build FlowTask.sln` 0 警告 0 错误；`dotnet test FlowTask.sln` 273 通过 / 0 失败（2026-09-27）。
- Manual: 所有者 2026-09-27 用 `preview/bugfix/startup-theme-no-flash/FlowTask.exe` 验收，原话「没问题」。
- Not run or not covered: 窗口几何（非目标）

## Risks and open questions

- Owner: 无待裁决项。
- Blocker or trigger: 无。

## Lessons learned

- 「先读再刷」如果挂在 `Opened` 上，窗口已经可见，验收会通过「最终对了」，但首帧仍是错的。

## Related documents

- SPECs: [spec-appearance-persist[DONE]](./spec-appearance-persist[DONE].md)
- Rules: `AI_CONSTITUTION.md` Article 2；`docs/rules/workflow-methodology.md`
