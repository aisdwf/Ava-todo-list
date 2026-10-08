# spec-theme-bound-decoration-colors: 修饰色绑定主题

## Metadata

- **ID**: spec-theme-bound-decoration-colors
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf（执行：Claude）
- **Created Date**: 2026-10-01
- **Last Updated**: 2026-10-01

## Why

用户原话（2026-10-01）：「project 和各项 tasks 的装饰，看起来还是更倾向于绿色和紫色……要么支持自定义配置这些颜色……要么就干脆绑定修饰和主题，每个主题有对应的修饰色。」

根因（Design Wrong）：

1. 项目色是建项目时一次性写进 `Project.ColorHex` 的字面 hex。取色走 `AppearanceCoordinator.PickPaletteColor`，循环使用已不在设置页的旧 4 色 `AccentPresets`（蓝 / 紫 `#A78BFA` / 绿 `#34D399` / 橙）。Default 占 `SortOrder` 0，所以第 1 个用户项目必为紫、第 2 个必为绿。界面经 `HexToBrushConverter` 直接画 hex，主题无从介入；浅色模式下也用 `DarkHex`。
2. 引导动画的项目色点借用 `PriorityMediumBrush` / `PriorityLowBrush`（琥珀 / 绿）。
3. 9 个主题中 8 个共用同一套优先级色，P3 恒为绿。
4. Fluent 原生控件（`RadioButton`、`CheckBox`、`ComboBox`、`Calendar` 选中日）读 `SystemAccentColor*`，本应用从未覆写，不跟主题强调色。
5. `MainWindow.axaml` 引用了未定义的 `DangerBrush`（176、774）和 `SurfaceBrush`（699、770）。Avalonia 静默跳过，错误文字和弹层底色实际未按设计着色（Code Wrong）。

## What

owner 裁决（2026-10-01，问答原话）：

| 问题 | 裁决 |
| --- | --- |
| 方向 | 「A 绑定主题」 |
| 项目之间是否用颜色区分 | 「统一单色」 |
| 优先级色 | 「保持语义色，并且 task 的修饰色可以通过优先级放大，而不是完全保持全局默认」 |
| 任务行修饰随优先级 | 「强度梯度（强调色→红）」 |
| `Project.ColorHex` | 「移除字段」 |
| Fluent 强调色、未定义键 | 「一起修」 |
| Q1 P 微标签色 | 「a」：保持红 / 琥珀 / 绿 |

据此：

1. **项目色移除**：删 `Project.ColorHex`、`DefaultProject.ColorHex`、`PickPaletteColor`、`HexToBrushConverter`。侧栏色点、任务行元信息色点、小窗项目下拉色点统一用 `AccentBrush`，随主题和昼夜变化。SQLite 旧列保留不读写，无迁移；种子 SQL 去掉该列。
2. **任务行色条改为优先级强度梯度**：`ProjectStripe` 更名为 `PriorityStripe`（含义已变，Article 5）。
   - P3 = `AccentGlowBrush`（强调色 34% / 28%）
   - P2 = `AccentBrush`
   - P1 = `PriorityHighBrush`（语义红）
   - 色条不再依赖 `HasProject`，所有任务都显示。悬浮提示改为优先级名称。
3. **P1/P2/P3 微标签**：不改，保持语义色红 / 琥珀 / 绿（Q1 = a）。
4. **Fluent 强调色**：`ApplyPalette` 按主题强调色写 `SystemAccentColor` 与 `SystemAccentColorDark1..3` / `Light1..3`。
5. **未定义键**：`DangerBrush` 改为既有 `PriorityHighBrush`，`SurfaceBrush` 改为既有 `CardSurfaceBrush`。复用权威令牌，不新造别名（Article 6）。另加测试，扫描所有 `.axaml` 的 `DynamicResource` 颜色键必须有定义，防止再次静默失效。
6. **清理死状态**：`PickPaletteColor` 是旧 4 色 `AccentPresets` 唯一的活消费者。随之删 `AccentPresets`、`ApplyAccent`、`FindAccent`、`MainViewModel.SelectedAccent` / `AccentPresets` 及对应测试。这些自 spec-settings-master-detail-and-theme-presets 撤下强调色选择后就是死状态。
7. **引导场景**：`GuideScenes.cs` 中项目色点改为 `AccentBrush`。
8. **文档**：更新 `design-visual-language.md` §2.2、§7 末句，以及 `design-domain-contract.md` 的 `ColorHex` 行。

## Non-goals

- 不提供自定义修饰色、色盘或修饰色预设（owner 选 A）。
- 不改各主题的强调色、表面色、文字色取值。
- 不改优先级语义色本身的色值（owner：「保持语义色」）。
- 不改 Tag 相关残留（`TagChip` 样式归 spec-remove-tag-feature 处理）。
- 不做数据库列删除迁移。

## Constraints and decisions

- Article 6：修饰色只从主题令牌派生，不建第二张色表。
- Article 5 / 10：含义已变的 `ProjectStripe` 更名；死状态整体删除，不留空壳。
- rule-no-invented-user-behavior：以下为 `[推断]`，需 owner 预览确认：
  - **[推断]** P3 用 `AccentGlowBrush` 而非 `AccentSubtleBrush`。依据：3px 宽色条上 18% 不透明度在深色底几乎不可见，34% 才能和 P2 拉开梯度又不消失。
  - **[推断]** 色条宽度三档保持 3px，强度只靠颜色表达。依据：变宽会让标题起始位置随优先级抖动，破坏列对齐。
  - **[推断]** 色条悬浮提示由项目名改为优先级名称。依据：色条不再表示项目，项目名已在元信息行显示。
- BR-1：本任务改变色条含义与色点颜色，不改手势、位置、快捷键。引导文字不变，场景色点需改。

## Acceptance criteria

- [x] 切换 9 个主题 × 昼夜，侧栏项目色点、任务行元信息色点、小窗项目色点都等于当前主题强调色，不再出现固定紫 / 绿。
- [x] 任务行色条：P1 红、P2 强调色、P3 浅强调色，切主题即变。
- [x] P1/P2/P3 微标签仍为红 / 琥珀 / 绿（Q1 = a，无改动）。
- [x] 关闭弹层的单选框、复选框，以及日历选中日，使用主题强调色。
- [x] 新建项目报错文字、启动错误条为语义红；关闭弹层、错误条底色为卡片色。
- [x] 代码中不再有 `ColorHex`、`PickPaletteColor`、`HexToBrushConverter`、`AccentPresets`。
- [x] 旧库（含 `ColorHex` 列）启动、新建项目、读写正常。
- [x] build 0 warning / 0 error；测试不低于 315 通过基线（删减的测试以新增测试补足，净数在 Verification 记录）。
- [x] owner 预览通过（2026-10-01，owner：「合入dev」）。

## Staged plan

1. **数据层**：删 `Project.ColorHex`、`DefaultProject.ColorHex`，种子 SQL 去列；补旧库兼容测试（预建含 `ColorHex` 列的旧表，再经仓储初始化、插入、读回）。
2. **外观协调层**：删 `AccentPresets` / `ApplyAccent` / `FindAccent` / `PickPaletteColor`；`ApplyPalette` 写 `SystemAccentColor*`；更新 `AppearanceCoordinatorTests`。
3. **ViewModel**：删 `ProjectItemViewModel.ColorHex`、`TaskRowViewModel.ProjectColorHex`、`MainViewModel.SelectedAccent` / `AccentPresets`、`CreateProjectViewModel` 取色；更新相关测试。
4. **视图与样式**：色点改 `AccentBrush`；`PriorityStripe` 三档样式；P 标签按 Q1；`DangerBrush` / `SurfaceBrush` 替换；删 `HexToBrushConverter`。
5. **防回归测试**：`DynamicResource` 键定义扫描测试；色条档位映射测试。
6. **引导与文档**：`GuideScenes.cs` 色点；design 文档两处；本 SPEC Change checklist。
7. **验证**：build + test，发布预览 `preview/feature/theme-bound-decoration-colors/FlowTask.exe`。

## Change checklist

- [x] `src/FlowTask.Core/Models/Project.cs`、`DefaultProject.cs`：删 `ColorHex`，实体注释说明为何没有项目色
- [x] `src/FlowTask.Infrastructure/Persistence/SqliteProjectRepository.cs`：种子 SQL 去 `ColorHex` 列
- [x] `src/FlowTask.Desktop/Appearance/AppearanceCoordinator.cs`：删 `AccentPresets` / `ApplyAccent` / `FindAccent` / `PickPaletteColor`；新增 `ApplyFluentAccent`（经 `FluentTheme.Palettes[variant].Accent` 驱动 `SystemAccentColor*`）
- [x] `src/FlowTask.Desktop/ViewModels/MainViewModel.cs`、`ProjectItemViewModel.cs`、`TaskRowViewModel.cs`、`Actions/CreateProjectViewModel.cs`：删颜色状态与死状态
- [x] `src/FlowTask.Desktop/Converters/DueDateConverters.cs`：删 `HexToBrushConverter`
- [x] `src/FlowTask.Desktop/Views/MainWindow.axaml`：色点去绑定；`ProjectStripe` → `PriorityStripe` + P1/P2 类 + 优先级提示；`DangerBrush` → `PriorityHighBrush`；`SurfaceBrush` → `CardSurfaceBrush`（关闭弹层改由 `SettingsCard` 样式供底色）
- [x] `src/FlowTask.Desktop/Views/QuickCaptureWindow.axaml`：项目下拉色点改用 `ProjectDot` 样式
- [x] `src/FlowTask.Desktop/Styles/EditorialStyles.axaml`：`ProjectDot` 填 `AccentBrush`；`PriorityStripe` 三档
- [x] `src/FlowTask.Desktop/Views/Guide/GuideScenes.cs`：4 处项目色点改 `AccentBrush`
- [x] 测试：`AppearanceCoordinatorTests`（重写强调色相关 4 项，新增 Fluent 强调色）、`ProjectInteractionTests`、`MainViewModelTests`、`SchemaMigrationTests`（旧库兼容）、新增 `DecorationColorTests`（键定义扫描 + 真实主窗色条 / 色点）
- [x] `docs/design/design-visual-language.md`（§2.4 别名说明、新增 §7.1 修饰色）、`design-domain-contract.md`（§4.1 删字段并记修订）、`design-interaction-principles.md`（「点击色条改项目」推断加修订注）
- [x] Guide: not affected — 指南文字未提及项目色或色条，手势与位置不变；只把 `GuideScenes` 里的项目色点改为强调色，tour 版本不升

## Progress log

### 2026-10-01

- Completed: 根因调查（explore 子代理，结论见 Why）；owner 方向问答；建工作树 `feature/theme-bound-decoration-colors`；本 SPEC 草稿。
- Decisions: 见 What 表格。
- owner 裁决 Q1 = a 并确认 7 步计划；SPEC 转 `[IN-PROGRESS]`。
- Phase 1–6 完成。
- 决策：Fluent 强调色走 `FluentTheme.Palettes[variant].Accent`（`ColorPaletteResources` 公开入口，自动派生 Dark1..3 / Light1..3），不手写 7 个 `SystemAccentColor*` 键。
- 决策：`AppearanceOption` 保留，仅作 `ThemePreset.Accent` 的深浅色值载体。
- 决策：原测试 `CreateProject_AssignsDistinctColorsToConsecutiveProjects` 断言的前提被 owner 裁决推翻，改为保留其仍有效的排序断言。
- 变异验证：把一处 `PriorityHighBrush` 临时改回 `DangerBrush`，`EveryDynamicColorKeyInXaml_IsDefined` 失败；还原后通过。
- owner 预览通过（「合入dev」），SPEC 关闭为 `[DONE]`，随代码在任务分支提交后合入 `dev`。
- Current resume point: 无，已关闭。
- Subagent/task references: explore `ses_f1376f0c6ffeZYvyYcRmtKKB5b`，主题与修饰色现状调查，只读。

## Verification

- Automated（2026-10-01）：`dotnet build --no-incremental` 0 warning / 0 error；`dotnet test` 329 / 329 通过（基线 315；本任务删 6 增 9，净 +3，其余增量来自 dev 上已有测试）。
  - 删除：`AccentPresets_ProvideFourDistinctOptions`、`PickPaletteColor_CyclesThroughAccentPresets`、`ApplyAccent_MutatesAllDerivedBrushes`、`ApplyAccent_PreservesUnrelatedTokens`、`SelectedAccent_DefaultsToFirstPreset`、`CreateProject_AssignsDistinctColorsToConsecutiveProjects`。
  - 替代 / 新增：`ThemePresets_AreUniqueAndOwnTheirAccent`、`ApplyThemePreset_MutatesAllAccentDerivedBrushes`、`ApplyThemePreset_PreservesUnrelatedTokens`、`ApplyThemePreset_DrivesFluentSystemAccent`、`AppearanceSelections_DefaultToFirstPreset`、`CreateProject_AppendsConsecutiveProjectsInOrder`、`LegacyProjectsTableWithColorHex_StillSeedsCreatesAndReads`、`EveryDynamicColorKeyInXaml_IsDefined`、`TaskRowStripeAndProjectDot_FollowPriorityAndTheme`。
- Manual: owner 预览 `preview/feature/theme-bound-decoration-colors/FlowTask.exe`，2026-10-01 通过。
- Not run or not covered:
  - Fluent 控件（单选、复选、下拉、日历选中日）实际渲染色：测试只断言 `SystemAccentColor` 资源值，控件模板是否全部读它需人工确认。
  - 3 条 `[推断]`（P3 透明度、色条同宽、提示改为优先级名）需 owner 目视判断。
  - 浅色模式下的色条 / 色点：真实主窗测试只跑了深色。

## Risks and open questions

- **Q1（已裁决 2026-10-01：a）**：P1/P2/P3 微标签用什么颜色？候选为 (a) 保持红 / 琥珀 / 绿；(b) 与色条同一梯度；(c) 红 / 琥珀 / 中性灰。owner 选 a。
- 风险（已由测试证实）：旧库 `ColorHex` 列由 `CreateTableAsync<Project>()` 建为可空列，不写该列的种子与插入成功，旧行读回正常（`LegacyProjectsTableWithColorHex_StillSeedsCreatesAndReads`）。
- 残留（不在本任务范围）：`EditorialStyles.axaml` 的 `TagChip` 孤立样式归 spec-remove-tag-feature 处理。

## Lessons learned

- 用户数据里存字面颜色，会让主题系统永远够不到它。展示色应存语义（或不存），由主题解析。
- 未定义的 `DynamicResource` 键静默失效，需要机器检查兜底（现由 `EveryDynamicColorKeyInXaml_IsDefined` 承担）。
- 撤下 UI 入口时（强调色选择）没有追查数据的其余消费者，留下的 `AccentPresets` 继续给项目取色，成了本次问题的直接来源。撤功能时应把它的数据源一起追到底。

## Related documents

- SPECs: [spec-settings-master-detail-and-theme-presets[DONE]](./spec-settings-master-detail-and-theme-presets[DONE].md)、[spec-unified-svg-icons[DONE]](./spec-unified-svg-icons[DONE].md)、[spec-editorial-and-ripple-theme[DONE]](./spec-editorial-and-ripple-theme[DONE].md)
- Design: `docs/design/design-visual-language.md`（§7.1）、`docs/design/design-domain-contract.md`（§4.1）、`docs/design/design-interaction-principles.md`
- Rules: `docs/rules/rule-no-invented-user-behavior.md`、`docs/rules/project-rules.md` BR-1
