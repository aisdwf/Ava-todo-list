# spec-due-date-calendar: 到期日三来源录入、日历、默认偏移设置；主窗移除今日聚焦

## Metadata

- **ID**: spec-due-date-calendar
- **Type**: complex
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-15
- **Last Updated**: 2026-09-16

> ## ✅ 闭环状态
>
> **本 SPEC 已获用户确认开工，并完成机器门禁 + 基础初版人工验收（2026-09-16）。**
> 状态 `done`。未实现项见 §6 Deferred Items。

**上游依据**：
- 需求：[REQUIREMENTS](../../requirements/REQUIREMENTS.md) R-2.2（deadline）
- 设计：[design-interaction-principles](../../design/design-interaction-principles.md) §4.1 / §4.3 / §5 / §8
- 领域：[design-domain-contract](../../design/design-domain-contract.md) §3（`DueDate` 日历日语义；「今日」查询语义**保留在仓储层**）

**用户本轮原话与裁决（2026-09-15）**：

> 「优先级最高的是到期日，日历实现、默认1天后和设置都还没有」
>
> 「快捷项的目的是是否启用 deadline，但是具体时间限制可以用设置修改」
>
> 「今日聚焦几乎没有什么联动性。我的建议是直接从主界面删除，
> 在后续小窗开发的时候，展示环节可以展示各个项目以及今日聚焦。」

| 议题 | 裁决 |
| :--- | :--- |
| 创建时自动写入 DueDate | **否**；默认 `null` |
| 快捷预设 | 仅「启用默认到期」+「清除」 |
| 默认偏移 N | 设置可改，默认 **1**，范围 **1–30** |
| 持久化 | SQLite `AppSettings` 表 |
| 行内改期 | **点击行上到期文案**弹出同一套控件 |
| 主窗「今日聚焦」 | **本轮从主界面删除**；展示能力推迟到小窗 SPEC |

---

## 0. 新会话接手须知

1. 勿重新发明「创建自动 +1 天」或 design 原文里的「今天/本周末/下周」全套预设。
2. 现状：`DueDate` 字段、仓储归一化、`SetDueDateAsync`、行上展示转换器**已有**；
   编辑态仍是强制 `yyyy-MM-dd` 的 `TextBox` —— 必须替换。
3. 主窗侧边栏仍有「今日聚焦」入口（`MainWindow.axaml` / `TaskFilter.Today` /
   `ViewSelection.Today` / `GetTodayTasksAsync` 调用链）。本轮**去掉主窗入口与筛选态**；
   `GetTodayTasksAsync` **保留**（小窗日后复用），不得当死代码删掉。
4. 设置页只加「默认到期偏移」；外观偏好持久化仍是独立推迟项，但存储应走同一 `AppSettings`。

---

## 1. Why

### 1.1 到期日录入

| 现状 | 问题 |
| :--- | :--- |
| 编辑态纯文本 `yyyy-MM-dd` | 用户否定强制完整格式手输 |
| 无日历 / 无快捷预设 | design §4.3 未落地 |
| 创建区无到期配置 | 违反「配置常驻」 |
| 无偏移设置 | 用户明示「具体时间限制可以用设置修改」 |

### 1.2 主窗「今日聚焦」

用户原话：「今日聚焦几乎没有什么联动性」。建议直接从主界面删除，
小窗展示环节再承载「各项目 + 今日聚焦」。

**本 SPEC 采纳**：主窗去掉该视图入口；**不**在本轮做小窗列表改造
（小窗仍受 design「大窗口差不多了再开始迭代」约束）。

### 1.3 Attribution

`Design Wrong` / `Code Incomplete` —— UI 未落地 §4.3；
主窗「今日」视图与到期录入脱节，用户判定联动不足。

---

## 2. What

### 2.1 目标

1. **三来源同步控件**：快捷预设 / 日历 / 纯数字，操作同一 `DateTime?`。
2. **快捷语义**：启用或关闭 deadline；具体日靠日历/数字。
3. **设置**：`DefaultDueOffsetDays`，默认 1，范围 1–30，SQLite `AppSettings` 持久化。
4. **创建不自动写入**：未点预设、未选日期 → `DueDate = null`。
5. **行内就地改期**：点击行上到期文案弹出同一控件；展开编辑内**不再**保留纯 `yyyy-MM-dd` TextBox。
6. **主窗移除「今日聚焦」**：侧边栏入口、选中态、标题文案、筛选分支一并去掉。
7. 解析失败**保留原值** + 轻量提示（禁止静默清空）。

### 2.2 快捷预设（已确认）

| 预设 | 行为 |
| :--- | :--- |
| 启用默认到期 | `DueDate = clock.Today + N`（N 来自设置，默认 1） |
| 清除 | `DueDate = null` |

不做：今天 / 本周末 / 下周（关闭 TODO(due-presets-extra)）。

### 2.3 数字快速输入（采纳 design §4.3 [推断]）

| 输入 | 含义 |
| :--- | :--- |
| `10` | 本月 10 日 |
| `0310` | 今年 3 月 10 日 |
| `260310` / `20260310` | 2026-03-10 |
| 非法 | **保留原值** + 轻量提示 |

`yyyy-MM-dd` 可作为合法粘贴形式，但不得作为唯一入口。

### 2.4 放置位置

| 位置 | 变更 |
| :--- | :--- |
| 顶部创建区 | 常驻到期日控件；创建时带入当前选中值（可仍为 null） |
| 任务行 | 点击到期文案 → 弹出同一控件；无到期时可点占位入口启用 |
| 展开编辑 | 移除 `yyyy-MM-dd` TextBox（避免双入口） |
| 设置页 | 「默认到期偏移（天）」 |
| 侧边栏 VIEWS | **删除「今日聚焦」**；保留「全部」「已完成」等其余入口 |
| 小窗 | 本轮**不做**到期录入，也**不做**今日列表展示 |

### 2.5 设置持久化（已确认：方案 A）

SQLite 新增 `AppSettings`（key/value），至少：

| Key | 值 | 默认 |
| :--- | :--- | :--- |
| `default_due_offset_days` | 整数 1–30 | `1` |

### 2.6 主窗移除「今日聚焦」（已确认）

**做**：
- 去掉侧边栏「今日聚焦」按钮与 `IsTodayFilterSelected` 绑定
- 从 `TaskFilter` / `ViewSelectionKind` / `ViewSelection` 移除 `Today`（或保留枚举但 UI/命令不可达 —— **偏好直接删除**，避免死分支）
- `MainViewModel` 筛选分支不再调用 `GetTodayTasksAsync`
- 相关单测改为断言主窗无此入口 / 默认视图为「全部」

**保留**：
- `ITaskRepository.GetTodayTasksAsync` 与仓储实现（领域定义仍有效，供小窗复用）
- 行上「今天到期 / 逾期」展示样式（与视图入口无关）

**登记推迟**（Article 4）：
- **TODO(quick-capture-today): [待小窗 SPEC]** 小窗展示「各项目 + 今日聚焦」；
  触发条件：主窗到期录入闭环且用户启动小窗迭代。

### 2.7 非目标

| 不做 | 理由 |
| :--- | :--- |
| 创建时自动写入 DueDate | 用户已否决 |
| design 全套额外预设 | 用户已否决 |
| 提醒 / 重复规则 | REQUIREMENTS §6 |
| 小窗到期录入或今日列表 | 显式推迟到小窗 SPEC |
| 时刻精度 | 未裁决；维持日历日 |
| 外观主题持久化 UI | 独立推迟；仅复用 AppSettings 表结构 |

### 2.8 影响面

**生产**：
- `MainWindow.axaml`（创建区、行上到期、侧边栏今日、编辑区 TextBox）
- `MainViewModel.cs` / `ViewSelection.cs` / `TaskFilter`
- `TaskRowViewModel.cs`
- `DueDateConverters.cs`（编辑转换器降级或删）
- 新增：到期控件 VM +（可选）UserControl / 弹出层
- 新增：`AppSettings` 模型 + 读写（可挂现有 SQLite 连接）

**测试**：解析、三来源同步、偏移读写、创建不自动写、解析失败保留、主窗无 Today 入口

---

## 3. 分阶段实施计划（须你确认后开工）

- [x] **Phase 1: AppSettings + 偏移设置 UI**
  - [x] `AppSettings` 表与读写；默认 N=1；范围 1–30
  - [x] 设置页控件 + 测试
- [x] **Phase 2: 日期解析**
  - [x] 纯数字 / `yyyy-MM-dd`；失败保留原值；一律经 `IClock`
- [x] **Phase 3: 三来源控件**
  - [x] 启用默认到期 / 清除 / 日历 / 数字；同一 VM 属性
  - [x] 样式令牌核验
- [x] **Phase 4: 接入创建区 + 行内弹出**
  - [x] 创建区常驻；`AddTask` 使用控件当前值
  - [x] 行上点击到期 → 弹出；移除展开编辑内旧 TextBox
- [x] **Phase 5: 主窗移除今日聚焦**
  - [x] 删侧边栏入口与筛选态；改测试；保留 `GetTodayTasksAsync`
- [x] **Phase 6: 验证**
  - [x] `dotnet build` 0/0；`dotnet test` 基线不降（176 通过）
  - [x] 基础初版人工验收通过（2026-09-16）；本 SPEC 改名 `[IN-PROGRESS]`→`[DONE]`、索引已同步

---

## 4. 执行记录与上下文追踪

### Subagent Log

| Timestamp | Subagent | Task | Task ID | Outcome |
| :--- | :--- | :--- | :--- | :--- |
| — | — | 暂未派发 | — | — |

### 关键决策与状态增量

- **[2026-09-15]** 用户指定到期日为当前最高优先级；要求写 SPEC 等确认。
- **[2026-09-15]** 创建不自动写入；快捷项=启用/清除；N 可设置，默认 1。
- **[2026-09-15]** 确认：仅两快捷项；SQLite AppSettings；N∈[1,30]；行上点击弹出。
- **[2026-09-15]** 确认：主窗删除「今日聚焦」；小窗再承载展示（本轮不实现小窗）。
- **[2026-09-15]** 用户显式回复「确认开工」；SPEC 由 `draft` 转为 `in-progress`。
- **[2026-09-16]** 创建区日历改为默认收起、按需展开（用户反馈常驻占位过大）。
- **[2026-09-16]** 用户认定「至少能算一个基础的初版」，指示做基本结算；SPEC 转为 `done`。

### Phase 执行记录

**[2026-09-15] Phase 1-5 完成（ViewModel + 核心逻辑）**
- ✅ 创建 `DueDateEditorViewModel.cs`：三来源同步、快捷预设、解析失败保留
- ✅ 14 个单元测试通过（DueDateEditorViewModelTests）
- ✅ 集成到 `MainViewModel`：字段、属性、命令、AddTaskAsync 改造
- ✅ 移除 `ViewSelectionKind.Today`、`TaskFilter.Today`、`IsTodayFilterSelected`
- ✅ 从 MainWindow.axaml 删除「今日聚焦」按钮
- ✅ `App.axaml.cs` 注入 `SqliteAppSettingsRepository`
- ✅ 更新所有 ViewModel 测试（MainViewModelTests、ProjectInteractionTests）
- ✅ 构建：0 警告 0 错误；测试：176 通过（baseline 162 + 14 新增）

**[2026-09-15] Phase 4/6 UI 收尾完成（机器侧）**
- ✅ 创建区：启用默认到期 / 清除 / 数字 / 日历
- ✅ 行上「到期」按钮 → 弹出同一编辑器；展开编辑已移除 yyyy-MM-dd TextBox
- ✅ 设置页「默认到期偏移」NumericUpDown + 保存
- ✅ 构建 0/0；测试 **176** 通过
- ✅ 创建区日历默认收起、按需展开
- ✅ 用户认定基础初版可结算（2026-09-16）；改名 `[DONE]`、索引同步；**未 git 提交**（等用户指示）

---

## 5. 验证记录

### 机器门禁

- [x] 构建：0 警告 0 错误
- [x] 测试：基线不降（176 通过）
- [x] 解析失败保留原值用例（DueDateEditorViewModelTests 内已验证）
- [x] 主窗无 `Today` 筛选入口相关断言（通过枚举值删除）

### 人工验证（由用户执行）

- [x] 新建不点预设 → 无到期日 —— 2026-09-16 基础初版验收
- [x] 「启用默认到期」→ 今天 + N（默认 1） —— 2026-09-16 基础初版验收
- [x] 设置改 N 后再生效 —— 2026-09-16 基础初版验收
- [x] 日历与数字互相同步 —— 2026-09-16 基础初版验收
- [x] 非法数字 → 原日期仍在 + 有提示 —— 2026-09-16 基础初版验收
- [x] 清除 → 行上不显示到期 —— 2026-09-16 基础初版验收
- [x] 点击行上到期文案可改期 —— 2026-09-16 基础初版验收
- [x] **确认已无强制 `yyyy-MM-dd` 唯一入口** —— 2026-09-16 基础初版验收
- [x] **侧边栏已无「今日聚焦」**；全部 / 已完成仍可用 —— 2026-09-16 基础初版验收
- [x] 创建区日历默认收起、按需展开 —— 2026-09-16 用户反馈后落地并认可

---

## 6. Deferred Items

- **TODO(quick-capture-today): [待小窗 SPEC]** 小窗展示各项目与今日聚焦（用户原话）。
- **TODO(settings-store-unification)**: 外观偏好持久化复用 `AppSettings`，避免双轨。
- ~~TODO(due-presets-extra)~~：**已关闭** —— 用户确认不要额外预设。

---

## 7. Commit Attribution

- **Attribution**: `Design Wrong` / `Code Incomplete`
  —— UI 长期停留在强制 `yyyy-MM-dd` 手输，未落地已确认的三来源同步；
  「今日聚焦」主窗入口与到期录入脱节，用户判定联动不足后删除。
- **Lessons Learned**:
  - 录入控件的**默认可见面积**本身是产品决策：日历常驻创建区会被判定为「占视觉空间过大」，应按需展开。
  - 「启用 deadline」与「选具体日」要拆开：快捷项只负责开/关，偏移天数进设置，日历/数字负责具体日。
  - 主窗视图入口若无稳定联动，宁可删掉并 Deferred 到真正有场景的载体（小窗），不要留空壳筛选。

---

## 8. 结算说明（2026-09-16）

| 项 | 结果 |
| :--- | :--- |
| 机器门禁 | build 0/0；test **176** 通过 |
| 基础初版范围 | 三来源录入、默认偏移设置、行上改期、主窗删今日聚焦、日历按需展开 |
| SPEC / 索引 | `done`；文件名 `[DONE]`；`docs/specs/README.md` 已同步 |
| git 提交 | **未执行**（等用户指示） |
| 显式推迟 | `TODO(quick-capture-today)`、`TODO(settings-store-unification)` |
