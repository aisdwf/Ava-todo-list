# spec-editorial-and-ripple-theme: 大开大合排版质感、水波纹昼夜切换与个性化外观设置规范

## Metadata
- **ID**: spec-editorial-and-ripple-theme
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-14
- **Last Updated**: 2026-09-14
- **Closed Date**: 2026-09-14

---

## 1. Why

当前界面的小组件密闭感过强、表格化过重，缺乏现代高级设计的大气感。用户通过 `res/fc3a588b-f4c4-43dd-a4c3-a68ada115472.png` 提出了明确的视觉与交互意图：
1. **大开大合的气势**：借鉴现代高级播客/杂志排版（Editorial & Typography-First），大字号、强节奏、克制且宽广的留白；
2. **右上角单一昼夜切换圆纽**：移除生硬的双选长条，改为集成在右上角的一体化交互按键；
3. **水波纹扩散动效**：按钮点击时，颜色变化以径向圆环的形式从按钮位置向整个屏幕平滑扩散；
4. **个性化外观设置面板**：在右上角昼夜切换旁增加“设置 (⚙)”入口，点击展开或跳转外观设置页，允许用户自定义背景风格（Mica/深炭/纯黑/暗蓝）及强调主题色。

### 1.1 Root Cause: 为什么首轮实现没有达到设计效果

**Attribution: Code Wrong + Test Wrong（设计文档本身无误）。**

首轮实现产出的界面与 design-visual-language 描述严重不符，用户反馈“概念不错，实际效果根本没达到”。5 Whys 追溯：

1. **为什么界面看起来是生硬深黑块？** → 窗口背景、卡片底色、边框全部渲染为空或退化。
2. **为什么这些视觉属性没有生效？** → `MainWindow.axaml` 引用的资源键（`MicaBackgroundBrush` / `CardSurfaceBrush` / `ElevationBorderBrush`）在 `App.axaml` 中从未定义；`App.axaml` 实际定义的是另一套键名（`SolidBackgroundBrush` / `CardBackgroundBrush` / `CardBorderHighlightBrush`），两套词汇零交集。
3. **为什么会出现两套令牌词汇？** → 令牌定义与视图消费分别成文、无单一真源约束，视图作者凭记忆书写键名，无任何机制校验（**违反 Article 6**）。
4. **为什么排版、动效、微标签等概念也没落地？** → `MainWindow.axaml` 引用了 `NavPill` / `QuickAction` / `EditorialCard` / `EditorialInput` / `FluentCombo` / `PrimaryAction` / `CaptureTrigger` 共 7 个样式类，而项目内**从未定义任何 `<Style>`**，仅有一个裸 `<FluentTheme />`。Avalonia 对未匹配的样式选择器与未定义的 `DynamicResource` 均**静默跳过**，不报错、不警告。
5. **为什么静默失败没有被发现？** → 测试项目只引用 `FlowTask.Core` 与 `FlowTask.Infrastructure`，**完全没有覆盖 Desktop 层**；机器门禁只能验证“能否编译”，无法验证“是否渲染出设计意图”。

**结论**：根因是「声明了视觉契约但从未实现该契约」，叠加「静默失败的框架特性」与「UI 层零测试覆盖」，使缺陷在编译与测试两道门禁下均无法暴露。

---

## 2. What Changes

### 视觉与排版 (Editorial Style)
- **Hero Title & Editorial Stream**:
  - 任务标题升级为 18-20pt 现代无衬线粗体，主副标题节奏鲜明；
  - 弱化繁杂线条，采用大间距呼吸流与精致微标（Micro-labels）；
  - 优先级采用微型标签（P1 / P2 / P3）与高光色彩呼应；
- **右上角交互区 (Theme Toggle + Settings Button)**:
  - 放置一对精致的 Fluent 药丸按钮：`☀️/🌙 主题切换` 与 `⚙ 外观设置`；
  - 主题切换点击时触发按压微动并以按钮坐标为圆心启动全屏径向水波纹扩散遮罩（Circular Reveal Animation）；
- **外观个性化自定义 (Appearance Settings)**:
  - 具备独立抽屉或视图区域：
    - **背景风格预设**：Mica 晶体、深炭极简 (Deep Charcoal)、纯黑深渊 (OLED Black)、暮光深蓝 (Midnight Blue)；
    - **主题强调色选择**：经典 Windows 蓝、活力电光紫、青碧绿、日落橙。

### 2.1 本轮实际交付 (2026-09-14)

**新增文件**
| 文件 | 职责 |
| :--- | :--- |
| `src/FlowTask.Desktop/Styles/DesignTokens.axaml` | 设计令牌单一真源：色彩、排版尺度、圆角、间距、优先级色板、Fluent 覆写 |
| `src/FlowTask.Desktop/Styles/EditorialStyles.axaml` | Editorial 视觉语言实现层，补齐此前全部缺失的样式类 |
| `src/FlowTask.Desktop/Appearance/AppearanceCoordinator.cs` | 主题变体、强调色、窗口材质的集中切换入口与权威预设定义 |
| `src/FlowTask.Desktop/Converters/PriorityConverter.cs` | 优先级 → P1/P2/P3 标签文案与档位匹配 |
| `src/FlowTask.Desktop/Converters/EnumChoiceConverter.cs` | 枚举 ↔ 单选按钮双向映射 |
| `src/FlowTask.Desktop/Converters/ThemeIconConverter.cs` | 昼夜图标由 `IsDarkTheme` 单向派生 |
| `tests/FlowTask.Tests/ConverterTests.cs` | 转换器行为断言（8 项） |
| `tests/FlowTask.Tests/MainViewModelTests.cs` | 视图模型状态流转与命令断言（16 项） |

**视觉落地要点**
- 深色底改为 design-visual-language §1 指定的深炭灰 `#202022`，表面层 `#28282C`，中灰文本 `#8A8A93`，近纯白标题 `#F4F4F6`；
- 任务行**取消卡片描边**，改为透明底 + 20px 上下留白 + 发丝线分隔，仅悬停浮现极弱底色，落实 design-visual-language.1「去除臃肿生硬边框」；
- Hero 标题 44pt Bold 负字距；任务标题 19pt SemiBold；微标 10.5pt Bold + 1.6 字距；
- 优先级微标签 P1/P2/P3 接入 design-visual-language.1 定义的珊瑚红/琥珀/鼠尾草绿三色板；
- 勾选完成后标题透明度沉降至 0.4 并加删除线（0.25s 过渡）；
- 方形勾选框替换为描边圆环 `RingCheck`；优先级下拉框替换为 P1/P2/P3 分段直选；
- 新增空状态提示；删除按钮改为行悬停淡入，静息态保持极简。

**缺陷修复（本轮附带发现并修正）**
| 位置 | 缺陷 | 归因 |
| :--- | :--- | :--- |
| `MainViewModel.ApplyAccentStyle` | 强调色写入顶层字典，被主题字典遮蔽，换色完全无效 | Code Wrong |
| `MainViewModel.MaterialPresetChanged` | 事件无订阅者，四个材质按钮点击无反应 | Code Wrong |
| `MainViewModel.DeleteTaskAsync(string)` | 生成 `RelayCommand<string>`，视图传入 `TaskItem`，点击删除必抛异常 | Code Wrong + Test Wrong |
| `MainViewModel.ActiveCount/CompletedCount` | 声明后从未赋值，侧边栏计数恒为 0 | Code Wrong + Test Wrong |
| `MainViewModel` 筛选联动 | 单次导航点击触发 2-3 次数据库查询与集合重填，列表闪烁 | Code Wrong |
| `MainViewModel.ChangeFilter` | 目标筛选等于当前值时属性不变更，停留设置页点击当前导航项无响应 | Code Wrong（由本轮新增测试捕获） |
| `MainWindow.TriggerCircularRevealAnimation` | 18 步 `Task.Delay(12)` 手搓帧循环，卡顿且无淡出 | Code Wrong（**违反 Article 9**） |
| 同上 | 波纹色硬编码 `#202020`/`#F9F9FB`，与令牌形成第二份副本 | **违反 Article 6** |
| `QuickCaptureViewModel` | 三个布尔量表达单选状态，可同时为真/假 | **违反 Article 10** |
| `QuickCaptureWindow` | 窗口复用时 `Opened` 仅首次触发，再次唤起不聚焦输入框 | Code Wrong |
| 设置入口 | 做成左侧任务筛选项，与 SPEC 要求的右上角 ⚙ 不符；`ToggleSettingsCommand` 无人调用 | Code Wrong |

---

## 3. Phased Plan

- [x] **Phase 1: SPEC 呈报与文件规范化**（完成生命周期标注文件名，整合外观设置需求）；
- [x] **Phase 2: 实现右上角主题切换按钮与全屏水波纹扩散动效**；
- [x] **Phase 3: 依据参考图全面重构主列表为大开大合的 Editorial 排版**；
- [x] **Phase 4: 实现右上角外观设置面板（支持动态切换背景材质与强调色）**；
- [x] **Phase 5: 编译构建、单元测试与 `./run.sh` 启动验证**。

---

## 4. Progress & Key Decisions

### Key Decisions

- **[2026-09-14]** 设计令牌集中至 `Styles/DesignTokens.axaml` 并由 `App.axaml` 单点 `ResourceInclude` 合并。视图只允许引用此处键名，防止令牌词汇再次分裂。
- **[2026-09-14]** 强调色采用「种子色 + 链式派生」：仅 `AccentColor` 为运行时可变令牌，`AccentBrush` / `AccentSubtleBrush` / `AccentGlowBrush` 全部 `DynamicResource` 引用该色，换色无需逐一枚举下游笔刷。写入时同时更新 Dark 与 Light 两套主题字典，避免切换主题后强调色回退。
- **[2026-09-14]** 「设置页」从 `CurrentFilter` 枚举中拆出，改由独立的 `IsSettingsOpen` 表达。视图模式与数据筛选是正交概念，混用会导致状态组合非法。左侧导航移除重复的设置项（用户确认采用）。
- **[2026-09-14]** 材质/强调色选择器改用 `ListBox` 承载而非 `Button` 阵列：选中态是列表控件原生语义。此前尝试以 `Classes.Selected` + `MultiBinding` 表达跨作用域比较不被 Avalonia 支持，遂放弃并删除相应的 `AllEqualConverter`，不保留无主抽象。
- **[2026-09-14]** 水波纹改由 `Animation` + `TransformOperations` 驱动缩放，取代手搓帧循环。**实机验证捕获**：`Animation.RunAsync` 要求目标为 `Visual`，直接对 `ScaleTransform` 跑动画会抛 `InvalidCastException`；改为对 `Visual.RenderTransform` 属性整体插值。此缺陷编译期与单元测试均无法发现，仅靠 `dotnet run` 实机启动暴露。
- **[2026-09-14]** 测试项目新增对 `FlowTask.Desktop` 的引用。UI 层零覆盖是首轮缺陷得以潜伏的结构性原因，补齐后本轮即由测试捕获 `ChangeFilter` 的无响应缺陷。

---

## 4.1 故障修复轮次 (2026-09-14 第二轮)

用户在 macOS 实测反馈两个故障：**调整外观配置无任何变化**、**切换昼夜模式直接闪退**。二者均通过了上一轮的编译、54 项以外的既有测试与启动验证，属于典型的"机器门禁全绿但功能不可用"。

### 故障 A：外观配置切换无效

**Attribution: Code Wrong.**

5 Whys：
1. 点击强调色/材质按钮无视觉变化 → 令牌值未被改写。
2. 为什么未改写？→ `WriteThemeColor` 中 `Resources.ThemeDictionaries.TryGetValue(variant, ...)` 返回 false，方法静默 return。
3. 为什么返回 false？→ 顶层 `ThemeDictionaries` 为**空集合**（诊断实测 `count = 0`）。
4. 为什么为空？→ `App.axaml` 以 `MergedDictionaries` + `ResourceInclude` 挂载令牌，`ThemeDictionaries` 位于**被合并的子字典内部**，而非顶层资源上。
5. 为什么没被发现？→ 令牌**读取**路径经 `TryGetResource` 会穿透合并链，因此界面配色正常显示；只有**写入**路径失效。读正常、写失效的组合使问题在静态检查与视觉观察下均不可见。

**修复**：令牌拆分为 `Tokens.Shared.axaml`（尺度）/ `Tokens.Dark.axaml` / `Tokens.Light.axaml`，深浅两套直接挂载在 `Application.Resources.ThemeDictionaries` 上。`WriteThemeResource` 对非 `ResourceDictionary` 的挂载形式（如 `ResourceInclude`）建立**保留原内容的覆写层**，确保写入落地且不丢失基础令牌。

**附带修复**：主题字典内的 `DynamicResource` 引用不限定在本变体内解析 —— 实测浅色主题下 `AccentBrush` 会取到深色字典的 `#4CA0FF`。故令牌改用字面色值，运行时换色由 `ApplyAccentToVariant` 集中覆写全部派生笔刷。

**材质无变化另有独立成因**：`TransparencyLevelHint` 仅声明期望的透明等级，而窗口 `Background` 绑定为不透明的 `WindowSurfaceBrush`，材质被完全遮盖。现由 `MaterialOption.SurfaceOpacity` 承载不透明度语义（Mica 0.80 / Acrylic 0.62 / Blur 0.46 / Solid 1.0），窗口背景交由 `ApplyMaterial` 构建；侧边栏也从不透明底改为发丝叠加色。

### 故障 B：切换昼夜模式闪退

**Attribution: Code Wrong.**

根因：Avalonia 11 的 `Animation.RunAsync` **没有为 `RenderTransform` 注册 animator**。诊断实测异常原文：

> `InvalidOperationException: No animator registered for the property RenderTransform. Add an animator to the Animation.Animators collection that matches this property to animate it.`

该异常在 `MainWindow` 构造函数内的 `async` 按钮事件处理器中抛出，无 catch 边界，直接逃逸至进程顶层 → 点击即闪退。

`Animation.Animators` 集合在 11.2 已从公开 API 移除，官方推荐方案不可用。改用 `TransformOperationsTransition` 声明式过渡：由合成器插值，无需 animator 注册，同时保持 Article 9 要求的无 sleep 式时序控制。

**附带修复**：`AppearanceOption.Swatch` 原在静态字段初始化时构造 `SolidColorBrush`。`Brush` 继承 `Animatable`，构造函数校验线程，导致任何非 UI 线程首次触达 `AppearanceCoordinator` 都会因类型初始化器失败而崩溃。改为惰性创建。

### 为什么上一轮的"实机验证"没有拦住

上一轮的验证命令只做了「启动进程 → 等待 → 检查进程存活 + 扫描日志」。两个故障都**必须点击特定按钮才触发**，而脚本化点击在 macOS 上被辅助访问权限拒绝。验证的是启动，用户操作的是交互，覆盖面根本不重叠 —— 却被当作了通过的证据。

**教训**：不能以"进程没退出"充当交互功能的验证。无法脚本化的交互路径，必须靠自动化测试覆盖其**代码路径**，或明确交由人工测试并如实说明未验证。

### 本轮验证方式

引入 `Avalonia.Headless.XUnit` 与 `TestAppBuilder`，使测试可在真实 `Application` 实例与 UI 线程上断言资源解析和过渡行为。新增 24 项断言（共 54 项全通过），关键覆盖：

- `ThemeDictionaries_AreMountedAtTopLevel` — 锁定令牌挂载位置，直接防护故障 A；
- `ApplyAccent_MutatesAllDerivedBrushes` — 断言换色真正改写笔刷；
- `ApplyAccent_PreservesUnrelatedTokens` — 断言覆写层不吞掉基础令牌；
- `CoreTokens_ResolveDifferentlyPerVariant` — 断言深浅变体隔离，防护 DynamicResource 串味；
- `ApplyMaterial_SetsHintAndTranslucentBackground` — 断言材质档位真的半透明；
- `RevealTransition_DoesNotThrowOnRenderTransform` — 断言新动画方案不抛异常；
- `AnimationOnRenderTransform_IsUnsupported` — **反向锁定根因**，若重构改回 Animation 写法立即失败。

> 中间过程记录：首次尝试自建 UI 线程 + `Dispatcher.Invoke` 的 fixture 导致测试套件死锁（与 headless 消息循环相互等待），已废弃并改用官方 xUnit 集成。

---

## 5. Verification

### Machine Gate（第二轮实测结果）
- [x] `dotnet build FlowTask.sln` → 0 警告 0 错误
- [x] `dotnet test FlowTask.sln` → **54 项通过 / 0 失败**
- [x] `dotnet build -c Release /p:TreatWarningsAsErrors=true` → 0 警告 0 错误（rule-code-standards 门禁）
- [x] 令牌完整性静态核对 → 无未定义资源键；深浅主题各 38 键、无差集

> **未做实机交互验证**：脚本化点击在 macOS 上被辅助访问权限拒绝，
> 且"进程存活"不足以证明按钮交互正常（这正是上一轮漏判的原因）。
> 交互行为改由上述 headless 测试覆盖代码路径，最终观感确认交由人工。

### Human Verification（macOS 环境，用户已确认通过 — 2026-09-14）

用户结论：**「目前总体观感我满意了」**。整体视觉方向与两个故障的修复效果均通过人工验收。

- [x] 昼夜切换不再闪退，水波纹扩散与换肤过渡正常；
- [x] 外观设置的强调色与材质切换即时生效；
- [x] 任务流呈现 Editorial 大字号气势，整体观感达到设计意图。

> **注**：Mica 为 Windows 11 平台特性。本次验收在 macOS 完成，Mica 档位按
> `TransparencyLevelHint` 候选链回退至 AcrylicBlur。Windows 11 上的原生 Mica
> 观感仍待目标平台确认，但不阻塞本 SPEC 关闭 —— 回退链已由
> `MaterialPresets_CarryOpacitySemantics` 断言覆盖（末位必为 `None`）。

---

## 6. Deferred Items（显式追踪，禁止隐形债务 — Article 4）

> 本节事项已汇总至 [`docs/specs/README.md` 待办事项索引](../README.md)，
> 新会话可从索引直接进入，无需先定位到本 SPEC。

- **TODO(today-view): [2026-09-21] 「今日聚焦」视图当前恒为空，需单独立项设计后再实现。**
  - **现状**：`SqliteTaskRepository.GetTodayTasksAsync` 按 `DueDate` 过滤，但项目内**无任何 UI 路径写入 `DueDate`**，该视图必然返回空集。
  - **附带问题**：该方法直接调用 `DateTime.Today`，业务逻辑分支依赖系统时钟，**违反 Article 9**，需引入可注入的 `IClock` 抽象。
  - **为什么推迟**：用户明确要求「具体功能的 design 还很不合理，需要单独做一次 design 再开始修改」。补到期日输入涉及数据契约与交互设计变更，超出 spec-editorial-and-ripple-theme 的视觉范围（Article 3 要求契约变更先经设计批准）。
  - **Owner**: aisdwf
  - **触发条件**：新建 design-domain-contract（任务时间语义与到期日交互）并获批准后立项。

- **TODO(cleanup): [2026-09-21] 移除 `FlowTask.Core/Class1.cs` 与 `FlowTask.Infrastructure/Class1.cs` 模板残留空类。**
  - **为什么推迟**：与本轮视觉改造无关，混入会污染本次变更的意图边界（Article 10 禁止拼凑式修改）。
  - **Owner**: aisdwf

- **TODO(persistence): [2026-09-28] 外观偏好（主题、强调色、材质）当前仅存于内存，重启后回退默认值。**
  - **为什么推迟**：spec-editorial-and-ripple-theme 只要求「即时生效」，未要求持久化。持久化涉及配置存储位置与格式决策，应作为独立 SPEC 处理。
  - **Owner**: aisdwf
  - **已核销（2026-09-26）**：由 [spec-appearance-persist[DONE]](./spec-appearance-persist[DONE].md) 落地（命名主题 / 材质 / 昼夜；强调色选择器已撤下不单存）。

---

## 7. Commit Attribution

- **Attribution**: Code Wrong + Test Wrong（design-visual-language 设计描述本身准确，问题在实现与验证环节）
- **Lessons Learned**:
  1. **声明式 UI 框架的静默失败是主要风险源**。Avalonia 对未定义的 `DynamicResource` 与未匹配的样式选择器既不报错也不警告，"能编译"与"渲染正确"之间存在巨大鸿沟。凡引入新样式类或资源键，必须同步确认其定义存在。
  2. **设计令牌必须单点定义并被视图强制复用**。两套并行的令牌词汇是本次视觉全面失效的直接原因，且无任何机器手段可发现。
  3. **UI 层测试覆盖不是可选项**。测试项目未引用 Desktop 层，使命令签名错配、计数未赋值等确定性缺陷长期潜伏；补齐覆盖后立即捕获到一个新的交互缺陷。
  4. **「启动不崩」不等于「功能可用」，不可充当交互验证的证据**。
     第一轮曾以"启动进程 + 检查存活 + 扫描日志"作为实机验证并判定通过，
     但外观切换无效与昼夜切换闪退都**必须点击特定按钮才触发**，
     而脚本化点击在 macOS 上被辅助访问权限拒绝 —— 验证范围与故障范围毫不重叠。
     正确做法：无法脚本化的交互路径，要么用 headless 测试覆盖其**代码路径**，
     要么如实声明未验证并交由人工，**绝不能用进程存活冒充功能正常**。
  5. **静默失效有"读正常、写失效"这一隐蔽变体**。令牌嵌在 `MergedDictionaries` 内时，
     读取经 `TryGetResource` 会穿透合并链（界面配色正常），但写入顶层 `ThemeDictionaries`
     全部丢弃。此类缺陷在静态检查与肉眼观察下均不可见，只能靠断言运行时**写入后**
     的实际状态来捕获。
  6. **框架 API 的能力边界必须实验确认，不能靠推断**。`Animation` 支持 `Opacity`
     却不支持 `RenderTransform`；官方文档提示的 `Animation.Animators` 在 11.2 已移除。
     本轮通过一次性 headless 诊断程序逐个验证候选方案，避免了在错误路径上反复试错。
