# spec-modal-overlay-block-hover: 全局覆盖层挡住背景悬停高亮

## Metadata

- **ID**: spec-modal-overlay-block-hover
- **Type**: simple
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-27
- **Last Updated**: 2026-09-27

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **2026-09-27 Step 0 已确认**（所有者原话「确认」）。
> 分类 `simple`：只修主窗覆盖层命中，不改关闭策略 / 托盘 / 单例。

## Why

关闭确认、到期日这类主窗内覆盖层表示「必须先处理这件事」。指针在卡片上时正常；移到卡片外，后面的任务行、侧栏、项目行仍走 `:pointerover` 高亮。

根因是实现错误（code wrong）：覆盖层宿主 `Grid` 没有 `Background`（Avalonia 里无背景的 Panel 不参与命中）；遮罩是空的 Fluent `Button`，背景画在按内容尺寸的 `ContentPresenter` 上，空内容几乎没有命中面积，事件穿透到主内容。

## What

两处覆盖层改成铺满窗口的命中遮罩；任一层打开时主内容 `IsHitTestVisible=False`。点遮罩仍取消（关闭选择层 / 到期日弹出层）。

## Non-goals

- 不改关闭策略、托盘、单实例。
- 不另开 Window。
- 不改卡片内部控件样式。

## Constraints and decisions

- 对齐 `spec-close-to-tray` D9：主窗内覆盖层，不另开 Window。
- 到期日弹出层与关闭选择层同一套命中合同，避免只修一处后同类穿透复发。
- `IsBlockingOverlayOpen` 由两个覆盖层标志派生，XAML 用已有的 `!` 绑定，不新增转换器。

## Acceptance criteria

- [x] 关闭选择层打开时，指针在卡片外，任务行 / 侧栏 / 项目行不高亮。
- [x] 到期日弹出层同样不再让背景高亮。
- [x] 点遮罩仍取消对应覆盖层。
- [x] `dotnet build` 0 警告 0 错误；`dotnet test` 不回退。

## Change checklist

- [x] `MainWindow.axaml`：遮罩铺满命中；主内容随 `IsBlockingOverlayOpen` 关闭命中
- [x] `MainViewModel.cs`：`IsBlockingOverlayOpen`
- [x] `MainViewModelTests.cs`：覆盖层开闭驱动该标志
- [x] 本 SPEC + `docs/specs/README.md`

## Progress log

### 2026-09-27

- Completed: 从干净 `dev` HEAD 建 `bugfix/modal-overlay-block-hover`；落地命中遮罩；所有者预览原话「没问题」。SPEC 收为 `[DONE]`。
- Decisions: 空 Fluent Button 不可作遮罩；主内容在覆盖层打开时关闭命中。
- Current resume point: 无（已关闭）。

## Verification

- Automated: `dotnet build FlowTask.sln -v q --nologo` → 0 警告 0 错误；`dotnet test FlowTask.sln --nologo -v q` → 218 通过 / 0 失败。
- Manual: 所有者 2026-09-27 用 `preview/bugfix/modal-overlay-block-hover/FlowTask.exe` 验收，原话「没问题」。
- Not run or not covered: 无自动化命中测试（Avalonia 指针命中需 UI 会话）；靠人工拖指针验证。

## Lessons learned

Avalonia 覆盖层不能依赖「看不见但 Stretch 的空 Button」。Panel 无 `Background` 不命中；Fluent Button 背景在 `ContentPresenter` 上，无 Content 时命中面积接近零。全窗遮罩必须自己画出带画刷的命中面，模态语义再关掉后面内容的 `IsHitTestVisible`。

## Related documents

- SPECs: [spec-close-to-tray[DONE]](./spec-close-to-tray[DONE].md)；[spec-due-date-calendar[DONE]](../task-domain/spec-due-date-calendar[DONE].md)（到期日弹出层）
- Rules: `rule-no-invented-user-behavior`（高亮异常是所有者原话，不是推断）
