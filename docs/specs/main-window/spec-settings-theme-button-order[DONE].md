# spec-settings-theme-button-order: 主窗口设置与昼夜按钮对调

## Metadata

- **ID**: spec-settings-theme-button-order
- **Type**: simple
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-26
- **Last Updated**: 2026-09-27

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-26）**：非常小的修改，直接在 `dev` 完成；主窗口非设置页对调两个按钮，隐藏设置后日夜按键位置不变。

## Why

打开设置会隐藏齿轮按钮。原先顺序是「昼夜 | 齿轮」，齿轮消失后昼夜按钮左移，落到齿轮原来的位置。指针还在原处时，下一个控件已经换成昼夜，操作不符合直觉。

根因是布局决策（design wrong）：会消失的入口放在了固定入口的右侧，`StackPanel` 收起左侧空位后，存活按钮必然占坑。

## What

主窗口右上角非设置页顺序改为「齿轮 | 昼夜」。打开设置后只少齿轮，昼夜仍停在最右侧。

## Non-goals

- 不改设置页左侧返回入口。
- 不改昼夜水波纹切换逻辑。
- 不改按钮样式或图标。

## Constraints and decisions

- 所有者要求直接在 `dev` 落地，不开 `feature/*` 分支。
- 齿轮 `IsVisible="{Binding !IsSettingsOpen}"` 保持不变；只对调 XAML 子节点顺序。

## Acceptance criteria

- [x] 任务页右上角：左齿轮、右昼夜。
- [x] 打开设置后齿轮消失，昼夜仍在窗口最右侧，不滑到齿轮原位。
- [x] 关闭设置后两钮回到「齿轮 | 昼夜」。

## Change checklist

- [x] `MainWindow.axaml` 右上角药丸按钮对调
- [x] 本 SPEC + `docs/specs/README.md`

## Progress log

### 2026-09-26

- Completed: 对调落地并打预览；未提交。随后合入 `feature/project-managed-tasks` 冲掉工作区改动。
- Decisions: 会消失的齿轮放左侧，昼夜锚定最右。
- Current resume point: 已从会话记录恢复对调。

### 2026-09-27

- Completed: 恢复后所有者预览原话「没问题」；SPEC 收为 `[DONE]` 并提交 `dev`。
- Current resume point: 无（已关闭）。

## Verification

- Automated: `dotnet build FlowTask.sln -v q --nologo` → 0 警告 0 错误；`dotnet test FlowTask.sln --nologo -v q` → 197 通过。布局无单测。
- Manual: 所有者用 `preview/dev/FlowTask.exe` 核对任务页顺序与进设置后昼夜位置，2026-09-27 原话「没问题」。
- Not run or not covered: 无。

## Lessons learned

未提交的 `dev` 工作区改动会被后续合入覆盖。即便所有者要求直接改 `dev`，预览通过后应立刻提交。

## Related documents

- SPECs: [spec-editorial-and-ripple-theme](../visual-theme/spec-editorial-and-ripple-theme[DONE].md)
- Rules: `docs/rules/rule-spec-complete-before-merge.md`（本项由所有者明确要求在 `dev` 完成，作为该规则的例外）
