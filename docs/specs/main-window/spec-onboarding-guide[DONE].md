# spec-onboarding-guide: 首次使用聚光灯引导 + 操作指南

## Metadata

- **ID**: spec-onboarding-guide
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-29
- **Last Updated**: 2026-09-29

> ## ✅ 开工许可（rule-spec-review-gate）
>
> **用户已确认（2026-09-29）**：对 `[DRAFT]` SPEC 与计划原话「执行」。
> Q1–Q4 未单独答复，按 SPEC 中的建议值执行（Q1 延迟首显触发、Q2 指向空状态区、Q3 热键标签取实际注册结果、Q4 齿轮 tooltip 改「设置」），预览时由所有者复核。

## Why

所有者原话（2026-09-29）：「优化引导能力，初次使用添加提示，并且添加一个位置合适的放置操作指南的位置。注意动效和图片辅助理解，不可能是纯文字内容。参考各个软件的引入」。

现状（代码核实）：

- 无任何引导、帮助或"首次运行"标记；`AppSetting` 表没有相关键。
- 「关于」分区只有两行文字；右上角只有齿轮（tooltip 仍写「外观设置」）和昼夜按钮。
- 部分能力靠隐性手势才能发现：双击项目重命名、点击标题展开编辑、悬停整行才出现删除、到期日数字速记 `10` / `0310` / `20260310`、`Alt+Space` 呼出小窗。
- `docs/requirements/REQUIREMENTS.md` §1 L33 写明「无引导教程」。与本任务冲突，须先改需求层（见 Constraints）。

## What

1. **需求与设计先行**：
   - REQUIREMENTS §1 删去「无引导教程」，新增 R-6（引导与操作指南），附所有者原话。
   - `design-interaction-principles.md` §2.2 增加一条受限例外：首次引导遮罩。
2. **首次聚光灯引导（Coach marks）**：首次启动时在主窗之上显示暗色遮罩，挖空高亮真实控件，旁边的气泡含一段矢量动效、标题、一句说明、`上一步 / 下一步 / 跳过` 和步骤点。共 6 步，按所有者勾选：
   1. 新建任务：`NewTaskInput`，动效演示"输入 → 回车 → 落入列表"。
   2. 到期日与优先级：到期行和 P1–P3，动效演示输入 `0310` 变成日期徽章，点亮 P1。
   3. 编辑与完成：首个任务行（空列表时指向空状态区），动效演示圆环勾选、删除线沉降，以及点击标题展开编辑面板。
   4. 项目：侧栏「新建项目」，动效演示新建、选中高亮、双击变成输入框。
   5. 快捷小窗：侧栏 `CaptureTrigger` 和键帽，动效演示按键亮起、小窗从右下浮现；显示实际注册成功的热键。
   6. 主题与设置：齿轮、昼夜和新的 `?` 按钮，动效演示水波纹扩散，并提示操作指南入口。
3. **操作指南入口**：
   - 右上角新增 `?` 圆钮，放在「? | 齿轮 | 昼夜」最左侧，打开设置 → 「操作指南」分区。
   - 设置导航新增第 4 分区「操作指南」，放在「关于」之前。
4. **操作指南页**：参照 VS Code Walkthroughs，左侧是按功能分组的步骤列表，右侧是动效舞台加说明。覆盖首次引导全部 6 项，并补充：删除任务、项目删除（级联）、默认到期偏移、关窗与托盘、小窗键位（Ctrl+Tab / ↵ / Esc）。页顶有「重新播放新手引导」按钮。
5. **持久化**：新增 `AppSetting` 键 `Onboarding.CompletedVersion`（整数）。完成或跳过都写入当前引导版本号；引导内容改版时提高版本号，可再次触发。

## Non-goals

- 不做示例任务或示例项目，不写用户数据。
- 不引入 GIF、视频或截图资源，不新增 NuGet 依赖。
- 不做多语言，文案沿用中文硬编码（REQUIREMENTS §6）。
- 小窗本身不加引导（它 420 宽、无边框，空间不足）；由第 5 步和指南页讲解。
- 不改任何既有功能行为；Win+Alt+Space 回退标签的修正除外（见 Risks Q3）。

## Constraints and decisions

- **所有者裁决（2026-09-29，问答原选项）**：
  - 「同意修改并允许例外」：改 REQUIREMENTS，并允许引导遮罩作为 §2.2 例外。
  - 形式选「聚光灯逐步提示」。
  - 入口选「右上角 ? 按钮 + 设置内新分区」。
  - 配图选「XAML 矢量动画小场景」。
  - 引导覆盖选「新建任务、到期日与优先级、编辑与完成任务、项目、快捷小窗与快捷键、主题与设置」（全部 6 项）。
- **§2.2 例外的边界**：只用于首次引导和手动重播。
  - 任何一步都可按 `Esc` 或点「跳过」立即退出。
  - 不遮挡气泡所指的控件（挖空区）。
  - 不用于其它场景。
- **参照做法及理由**（REQUIREMENTS §4.4 要求说明为什么好）：
  - Figma、Linear 的 coach marks：直接指向真实控件，用户学到的就是之后要点的位置。缺点是内容一多就打断，因此限制为 6 步、每步一句话。
  - VS Code Walkthroughs：左侧目录、右侧媒体。用户可以按需跳读，适合常驻的完整参考。
  - 滴答清单、Things 的"随时重看"入口：引导被跳过后仍能找回。
- **视觉与动效**（`design-visual-language.md`）：
  - 只用既有 token，图标沿用 24×24、1.75 线宽的描边集，新增 `IconHelp`（问号圆），不做填充或彩色版本。
  - 动效只用 `Transitions`（Opacity、RenderTransform、Brush），CubicEaseOut，时长 160–520ms。Avalonia 11.2 的 `Animation` 不支持 `RenderTransform`（visual-language §5.1 已实验确认）。
  - 循环演示由 `DispatcherTimer` 驱动状态机切换属性，再由 Transitions 补间。这属于用户可见动画，符合 Article 9 的豁免。
  - 遮罩透明度沿用既有 `#73000000`；挖空区圆角沿用 `CardCornerRadius`。
- **架构**：
  - 遵守 TR-1：状态和步骤推进放在独立的 `OnboardingViewModel`，`MainViewModel` 只保留引用和薄命令；完成标记的持久化放在独立的 action 类。
  - 视图侧：`CoachMarkOverlay`（UserControl）用 `TranslatePoint` 取目标控件的边界，窗口尺寸变化时重新计算。
  - 目标控件通过附加属性 `OnboardingAnchor.Key` 标注，不在 ViewModel 里引用控件。
  - 引导期间遮罩要纳入 `IsBlockingOverlayOpen`，以阻断主内容悬停（spec-modal-overlay-block-hover）。
- **后续维护（所有者 2026-09-29 预览时提出）**：原话「后续功能更新这个部分也会涉及到更新，这一点是否需要在spec指明？」
  - 决定：这是一条长期义务，不写在本 SPEC 里。`[DONE]` SPEC 是任务的工作记录，后续任务不会回读；义务写进每个任务都必读的 `docs/rules/project-rules.md` **BR-1**，本 SPEC 只记录这项决定并链接过去。
  - BR-1 要求：凡改动用户可见交互的任务，同一分支内同步更新 `GuideCatalog`、对应场景和锚点；涉及首次引导的步骤时把 `OnboardingProgress.CurrentVersion` 加 1；在 SPEC 的 Change checklist 写一行 `Guide:`，说明已更新或不受影响及理由。
  - 机器门禁：`GuideMaintenanceTests`（锚点都有视图引用、生效日后新建的 SPEC 必须有 `Guide:` 行），另有 `GuideSceneRenderTests`、`OnboardingTests`、`CoachMarkOverlayTests` 覆盖场景、目录与锚点的一致性。
- **不改动的契约**：设置导航仍重置到 Appearance；「齿轮 | 昼夜」相对顺序不变（spec-settings-theme-button-order 的"会消失的在左"原则）。`?` 在设置页同样隐藏，并放在最左侧。

## Acceptance criteria

- [x] REQUIREMENTS 与 design-interaction-principles 已更新，引用所有者原话。
- [x] 全新数据库首次启动主窗时自动进入引导第 1 步。已完成或已跳过的，重启后不再出现。
- [x] 每步的挖空区准确框住目标控件；窗口缩放后仍然对齐。
- [x] 每步气泡都有循环矢量动效，不是纯文字；亮色和暗色主题下都清晰可读。
- [x] `Esc` 或「跳过」可在任意一步退出；`←` / `→` 可切换步骤。
- [x] 右上角有 `?` 按钮，顺序为「? | 齿轮 | 昼夜」；打开设置后 `?` 和齿轮消失，昼夜位置不动。
- [x] 设置导航有「操作指南」分区；页内每个功能条目都有动效舞台，「重新播放新手引导」可再次进入第 1 步。
- [x] 引导期间主内容不响应悬停和点击（遮罩阻断）。
- [x] build 0 警告 0 错误；test ≥ 基线 272 且新增测试全绿。
- [x] 所有者预览通过（2026-09-29 原话「预览通过」）。
- [x] 后续维护义务已写入 `project-rules.md` BR-1，并有机器门禁。

## Staged plan

1. **文档**：改 REQUIREMENTS（R-6）和 design-interaction-principles §2.2 例外。
2. **状态和持久化**：`OnboardingViewModel`（步骤模型、前进/后退/跳过/完成）、`Onboarding.CompletedVersion` 读写、`InitializeAsync` 接线，并补单测。
3. **动效组件**：`GuideSceneHost` 舞台加 11 个矢量小场景（代码拼装的线框，见 Decisions），由 DispatcherTimer 驱动循环状态，Transitions 补间；新增 `IconHelp`。
4. **聚光灯遮罩**：`CoachMarkOverlay`，负责挖空几何、气泡定位（自动选上下左右）、键盘操作、窗口缩放时重新对齐，以及 `IsBlockingOverlayOpen` 接线。
5. **操作指南页**：`SettingsSection.Guide`、导航项、左侧目录加右侧舞台、重播按钮、右上角 `?` 按钮。
6. **验证和预览**：build、test、`publish-preview.ps1`，交所有者预览。

## Change checklist

- [x] `docs/requirements/REQUIREMENTS.md`：§1 删去「无引导教程」，新增 §4.6 R-6，§7 差距表加行。
- [x] `docs/design/design-interaction-principles.md`：§2.2 增加受限例外条款。
- [x] `src/FlowTask.Desktop/ViewModels/GuideCatalog.cs`（新）：`GuideTopic`、`GuideSceneKind`、`OnboardingTargets`，以及文案单一来源。
- [x] `src/FlowTask.Desktop/ViewModels/OnboardingViewModel.cs`、`GuideViewModel.cs`（新）。
- [x] `src/FlowTask.Desktop/ViewModels/Actions/CompleteOnboardingViewModel.cs`（新）。
- [x] `src/FlowTask.Desktop/Services/OnboardingProgress.cs`（新）：`Onboarding.CompletedVersion` 解析与序列化。
- [x] `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`：`Onboarding` / `Guide` 属性、`InitializeAsync` 读取、`IsBlockingOverlayOpen`、`OpenGuide` / `StartOnboarding` 薄命令、`SettingsNavItems` 加 Guide、热键文案改为可观察属性。
- [x] `src/FlowTask.Desktop/ViewModels/SettingsSection.cs`：加 `Guide`。
- [x] `src/FlowTask.Desktop/Views/Onboarding/CoachMarkOverlay.axaml(.cs)`、`OnboardingAnchor.cs`（新）。
- [x] `src/FlowTask.Desktop/Views/Guide/SceneKit.cs`、`GuideScenes.cs`、`GuideSceneHost.cs`、`GuidePage.axaml(.cs)`（新）。
- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml(.cs)`：锚点标注、`?` 按钮、齿轮 tooltip、指南分区、遮罩层、首启触发。
- [x] `src/FlowTask.Desktop/Services/GlobalHotkeyService.cs`、`QuickCaptureHotkey.cs`（新）、`App.axaml.cs`：热键实际注册结果（Q3）。
- [x] `src/FlowTask.Desktop/Styles/Icons.axaml`：`IconHelp`、`GuidePointer`。
- [x] `src/FlowTask.Desktop/Styles/EditorialStyles.axaml`：CoachSpotlight、CoachBubble、GuideStage、CoachGhost、StepDot、GuideTopics、GuideBullet。
- [x] `tests/FlowTask.Tests/OnboardingTests.cs`、`GuideSceneRenderTests.cs`、`CoachMarkOverlayTests.cs`（新）。
- [x] `docs/rules/project-rules.md`：新增 BR-1（指南与功能同步维护）；`tests/FlowTask.Tests/GuideMaintenanceTests.cs`（新）作为门禁。
- [x] Guide: updated 全部 11 个条目（本任务新建指南）。
- [x] 所有者预览通过。

## Progress log

### 2026-09-29

- Completed:
  - 读完 Gate 2 文档；代码调研由 explore 子代理完成（task `ses_f16e7f9a3ffeZJdRvqTwW6NA18`）。
  - 所有者对 5 个问题作了裁决。
  - 建好 worktree `feature/onboarding-guide`。
  - 所有者原话「执行」，SPEC 转为 `[IN-PROGRESS]`。
  - Staged plan 1–6 全部完成，预览已发布。
- Decisions:
  - 场景用代码拼装线框（`SceneKit` + `GuideScenes`），不写 11 个 axaml。理由：场景是逐步改属性的脚本，写成 XAML 状态会成倍膨胀；拼装时直接复用同一套令牌和 `Icons.axaml` 几何，观感与真实界面同源。
  - 文案单一来源 `GuideCatalog`：首次引导的 6 步从指南 11 个条目中筛出（带锚点者），不另写一份（Article 6）。
  - 锚点用附加属性 `OnboardingAnchor.Key` 标注，锚点名为 `OnboardingTargets` 常量，XAML 用 `x:Static` 引用。
  - 挖空用单个 Border 的超大外扩 BoxShadow 压暗四周，压暗色沿用既有遮罩 `#73000000`。
  - 键盘在窗口级 Tunnel 拦截：遮罩期间焦点可能停在新任务输入框，回车不能漏进去建出任务。
  - 窗口高度低于 720 时，气泡舞台从 168 降到 120，保证 940×600 下气泡不越界、不压挖空。
- 所有者预览：原话「预览通过」。同时提出后续功能更新时指南也要更新，要求写明；已落为 `project-rules.md` BR-1 与 `GuideMaintenanceTests`（见 Constraints「后续维护」）。
- Current resume point: 无（已关闭）。

## Verification

- Automated（2026-09-29）：
  - `dotnet build FlowTask.sln --no-incremental -v q --nologo`：0 警告 0 错误。
  - `dotnet test FlowTask.sln --nologo -v q`：312 通过（基线 272，新增 40）。
  - 新增测试：
    - `OnboardingTests`：首次触发、完成或跳过后落盘并不再触发、旧版本再次触发、重播退出设置、步骤边界、遮罩阻断、目录覆盖、存值容错、热键文案。
    - `GuideSceneRenderTests`：11 个场景各跑一整轮加复位。
    - `CoachMarkOverlayTests`：真实主窗 headless 下的挖空与气泡位置、空列表回退、回车/←/Esc 不漏进输入框、`?` 打开指南。
- Manual: 所有者用 `preview/feature/onboarding-guide/FlowTask.exe`（全新 `flowtask.preview.db`）预览，2026-09-29 原话「预览通过」。Q1–Q4 的建议值随之确认。
- Not run or not covered: 动效观感、压暗程度、亮色主题下的可读性、真实 Win+Alt+Space 回退。headless 测试宿主未启用 Skia，不截帧。

## Risks and open questions

Q1–Q4 未单独答复，已按建议值实现（见开工许可），预览时请所有者一并复核：

- **Q1 [推断] 引导何时触发**：已实现。主窗 `InitializeAsync` 完成后延迟 400ms 开始；此时有阻断弹层、或启动出错时不播。托盘或小窗启动的情形不涉及：主窗在启动时总会 Show。Owner: aisdwf，预览复核。
- **Q2 [推断] 第 3 步列表为空**：已实现。锚点按「任务行 → 空状态区」顺序取第一个可见者，headless 测试覆盖两种情况。不创建示例任务。Owner: aisdwf，预览复核。
- **Q3 热键标签**：已实现。`GlobalHotkeyService.Registered` 记录实际命中组合，`MainViewModel.QuickCaptureHotkeyLabel` 据此显示，侧栏键帽与引导一致。与引导同一分支，提交时拆成独立 commit。Owner: aisdwf，预览复核。
- **Q4 齿轮 tooltip**：已改为「设置」。Owner: aisdwf，预览复核。
- **风险：挖空对齐**：已缓解。锚点按可见性选取，每次布局更新重新测量；`CoachMarkOverlayTests` 在 1180×780、940×600、有/无任务三种组合下断言：挖空包住锚点，气泡完整在窗内且不压挖空。
- **风险：MainWindow.axaml 膨胀**：指南页拆为 `GuidePage`，遮罩拆为 `CoachMarkOverlay`；MainWindow 只加锚点标注、`?` 按钮和两处挂载。
- **未验证**：真实 Win+Alt+Space 回退只能在 Alt+Space 被占用的机器上观察；本机未复现，仅有单测覆盖文案映射。

## Lessons learned

- 动效场景全靠运行时拼装，编译器查不出资源键拼错、过渡结构不匹配这类错误，而且异常会在计时器回调里逃逸到进程顶层。`GuideSceneRenderTests` 逐帧跑完每个场景，是这类错误唯一的自动拦截点，后续新增场景必须进这个 Theory。
- 挖空框的 Width/Height 带过渡，读控件属性会拿到插值中间值（首版测试据此误报"没框住"）。断言要读计算终值 `SpotTarget`，不要读动画中的属性。
- 布局断言只在默认窗口尺寸下跑是不够的：940×600 最小窗口下，第 2 步气泡越出窗口，是参数化测试加入最小尺寸后才发现的。

## Related documents

- SPECs:
  - [spec-settings-theme-button-order](./spec-settings-theme-button-order[DONE].md)
  - [spec-modal-overlay-block-hover](./spec-modal-overlay-block-hover[DONE].md)
  - [spec-viewmodel-command-decomposition](./spec-viewmodel-command-decomposition[DONE].md)
  - [spec-quick-window-hotkey-capture](../quick-capture/spec-quick-window-hotkey-capture[IN-PROGRESS].md)
- Requirements: `docs/requirements/REQUIREMENTS.md` §1、§4.4、§6
- Design: `docs/design/design-interaction-principles.md` §2、§9；`docs/design/design-visual-language.md` §2.4、§5、§7
- Rules: `docs/rules/technical-rules.md` TR-1；`docs/rules/rule-no-invented-user-behavior.md`；`docs/rules/project-rules.md` BR-1（后续维护义务）
