# spec-create-project-click-outside-confirm: 新建项目点外部确认

## Metadata

- **ID**: spec-create-project-click-outside-confirm
- **Type**: simple
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-26
- **Last Updated**: 2026-09-26

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-26）**：对齐改名的「点外部即确认」；非法名称显示短暂提示。

## Why

项目改名已在窗口级 Tunnel `PointerPressed` 上覆盖「点击不可聚焦空白也提交」，因为 Avalonia 的 `TextBox.LostFocus` 在点击 `Border` / `StackPanel` 等默认不可聚焦控件时不会转移焦点。新建项目输入框没有接入同一条路径，只能靠 Enter 确认，点外部无反应。

根因是实现遗漏（code wrong）：离开输入框即确认这一交互只接到了重命名，没有接到创建。

## What

- 点在新建项目输入框外（含不可聚焦空白）时提交创建，与改名同一窗口监听。
- Tab 等非指针失焦走 `LostFocus` 兜底，同样提交。
- Enter 仍创建；Escape 仍收起。
- 名称非法时展示 `ProjectName.Validate` 返回的原文案（到期日解析错误同款 `CaptionText` + `DangerBrush`），约 2.5 秒后自动消失；输入变化或收起时立即清除。

离开输入框时的分支（用户 2026-09-26 确认的复述 + 「非法则短暂提示」）：

| 输入 | 离开（点外部 / LostFocus） | Enter |
| :--- | :--- | :--- |
| 合法名称 | 创建并收起 | 创建并收起 |
| 空白 | 收起、不创建、不提示（视为放弃） | 不创建，提示「项目名称不能为空。」 |
| 非空但非法（超长） | 不创建，保持输入区，显示校验文案 | 同左 |

## Non-goals

- 不改项目重命名交互。
- 不引入通用 toast / snackbar 基础设施。
- 不改 `ProjectName` 校验规则本身。

## Constraints and decisions

- 校验文案唯一真源：`ProjectName.Validate`（Article 6）。
- 离开确认复用已有窗口级 `PointerPressed` Tunnel，不另做一套焦点 hack。
- 2.5 秒自动消失为 **[推断]**：用户要求「短暂」；对齐本窗主题转场量级（约 0.5–2.5s），非业务门闩（Article 9 允许面向用户的展示时长）。
- TR-1：离开提交的分支放进 `CreateProjectViewModel`，`MainViewModel` 只做薄委托。

## Acceptance criteria

- [x] 展开新建、输入合法名、点击侧边栏或任务区空白 → 项目被创建，输入区收起。
- [x] 展开新建、不输入、点击外部 → 输入区收起，无新项目，无错误文案。
- [x] 展开新建、输入超长名、点击外部或按 Enter → 不创建，输入区仍在，显示 `ProjectName.Validate` 文案，约 2.5 秒后文案消失。
- [x] Escape 仍收起并清空，无残留错误文案。
- [x] 改名的点外部确认行为不变。

## Staged plan

1. [x] 把离开提交与非法提示接入 `CreateProjectViewModel` / `MainViewModel`。
2. [x] 窗口 PointerPressed + LostFocus 接到离开命令。
3. [x] 输入框下展示短暂错误文案。
4. [x] 补单测；构建 + 全量测试。

## Change checklist

- [x] `CreateProjectViewModel`：非法走 `onInvalid`；新增 `ExecuteOnLeaveAsync`
- [x] `MainViewModel`：`CreateProjectError`、离开命令、输入变化清错
- [x] `MainWindow.axaml` / `.axaml.cs`：离开提交 + 错误展示 + 自动消失
- [x] `ProjectInteractionTests`
- [x] 本 SPEC + `docs/specs/README.md`

## Progress log

### 2026-09-26

- Completed: 所有者确认开工；离开提交与非法提示已接入；`dotnet build` 0 警告 0 错误；`dotnet test` 187 通过。所有者预览通过（2026-09-26「没问题」），SPEC 收为 `[DONE]` 后合入 `dev`。
- Decisions: 空白离开 = 放弃；非空非法 = 提示并保持输入；文案来自 `ProjectName.Validate`；提示 2.5 秒后由窗口 DispatcherTimer 清除。
- Current resume point: 无（已关闭）。

## Verification

- Automated: `dotnet build FlowTask.sln -v q --nologo` — 0 警告 0 错误；`dotnet test FlowTask.sln --nologo -v q` — 187 通过（2026-09-26）
- Manual: 所有者预览 `preview/feature/create-project-click-outside-confirm/FlowTask.exe` 通过（2026-09-26「没问题」）
- Not run or not covered: 窗口 PointerPressed 本身无独立 UI 测试，由 ViewModel 离开命令覆盖分支。

## Risks and open questions

None.

## Lessons learned

同一类「离开输入即确认」必须挂在窗口级 `PointerPressed` Tunnel 上，不能只接到某一个编辑框。新建项目当时只做了 Enter/Escape，因此复现了改名曾经的 LostFocus 盲区。

## Related documents

- SPECs: `spec-classification-ui`（新建入口）、`spec-viewmodel-command-decomposition`（TR-1）
- Rules: `rule-no-invented-user-behavior`、`technical-rules.md` TR-1
