# spec-quick-window-single-project-list: 小窗单项目列表 + 勾选

## Metadata

- **ID**: spec-quick-window-single-project-list
- **Type**: complex
- **Status**: draft
- **Owner**: aisdwf
- **Created Date**: 2026-09-16
- **Last Updated**: 2026-09-16

> ## ⛔ 开工前置条件（rule-spec-review-gate）
>
> **本 SPEC 仍为 `draft`。禁止改动 `src/` 与 `tests/`。**
> 须等用户显式确认。建议在 `spec-task-complete-before-archive` 落地后再做勾选接线，
> 否则勾选仍会「瞬间进归档」，重演用户已否定的行为。

**上游依据**：
- 需求：R-1.1 / R-1.2 / R-1.4；展示范围本轮裁决为**单项目**
- 设计：design-interaction-principles §8；归档交互见 design §7.3（由 complete-before-archive 修订）
- 用户原话：「还要能显示 todo list（单项目）和勾选操作」；「记忆上次项目」

**依赖**：
- 硬依赖（热键唤起）：`spec-quick-window-hotkey-capture`（可先用按钮唤起做 UI，但验收按热键）
- 硬依赖（勾选容错）：`spec-task-complete-before-archive`（完成≠归档）

**建议开工顺序中的第 3 份**（先 1 热键捕捉，再 2 完成/归档，再本份列表勾选）。

---

## 0. 新会话接手须知

1. 当前小窗无列表；主窗 `GetActive*` 查询排除 `IsCompleted`（在 archive SPEC 前会改）。
2. 「单项目」= 任一时刻只展示**一个**项目（或「未归属」）下的任务，不做多项目混合列表。
3. 默认项目：**记忆上次在小窗选择的项目**（用户裁决）。持久化位置见 §2.4。

---

## 1. Why

R-1.1 / R-1.2 要求小窗可查看并勾选。用户补充：

- 列表维度为**单项目**（早期演进方向，现升为初版范围）；
- 默认**记忆上次项目**。

Attribution：`Design Incomplete` —— §8 曾推迟小窗；列表范围曾为「全部未完成」推断，现被「单项目」原话取代。

---

## 2. What

### 2.1 窗口结构

- 上：输入区（由 hotkey-capture SPEC 负责语法；本 SPEC 不改解析）
- 下：可滚动任务列表（勾选圈 + 标题 + **[推断]** 项目色条可省略因已在单项目上下文）
- 高度随列表增长，设上限后滚动（**[推断]** 可见约 5–7 行；待试调，不阻塞开工）

### 2.2 列表数据

- 展示当前选中项目下、**未归档**任务（含「已完成未归档」，样式由 archive SPEC 定义）。
- 项目切换：**[待裁决]** 见 D1（快捷键 / 下拉 / 输入 `@` 切换是否联动列表）。
- 槽位为具体项目（含系统 **Default**）；废除 `ProjectId == null`（R-2.6）。

### 2.3 勾选

- 鼠标与键盘均可（R-1.4）；**[推断]** Space 切换当前行完成态。
- 行为必须调用与主窗同一套完成/取消完成命令（完成≠归档由另一 SPEC 保证）。
- **禁止**勾选后立即从列表移除（除非已归档）。

### 2.4 记忆上次项目

- 键：如 `AppSettings` 中 `QuickWindow.LastProjectId`（空串 = 未归属）。
- 小窗打开时恢复；用户切换项目时写入。

### 2.5 待裁决

| # | 议题 | 候选 |
| :--- | :--- | :--- |
| D1 | 项目切换 UI | A 列表顶下拉 / B 快捷键循环 / C 仅 `@` 补全间接切换 |
| D2 | 列表排序 | A 与主窗项目视图相同 / B 仅未完成在上、已完成未归档置底 |

### 2.6 非本 SPEC

- 全局热键、`@`/`#` 解析 → hotkey-capture
- `IsArchived` / 归档动作 → complete-before-archive
- 多项目总览 / 今日聚焦切片 → 仍为演进（due-date SPEC 的 TODO(quick-capture-today)）

---

## 3. How（确认前不执行）

1. 确认 archive SPEC 的查询语义已合并（活动列表含已完成未归档）。
2. 扩展 `QuickCaptureViewModel`：当前项目、任务集合、切换/勾选命令。
3. 改造 `QuickCaptureWindow` 布局；处理拖拽与列表点击冲突（archived design 曾标为高风险）。
4. 持久化 `LastProjectId`；打开时恢复。
5. 键盘：上下行、Space 勾选、Esc 隐藏。
6. 构建 + 单测 + 人工验收。

---

## 4. 验证

### 机器

- [ ] build / test 全绿

### 人工

- [ ] 小窗只显示当前项目任务
- [ ] 重启后恢复上次项目
- [ ] 勾选后呈完成态且仍可见；再勾可选取消
- [ ] 不打开主窗可完成查看与勾选

---

## 5. Deferred Items

| 事项 | 期限 | 触发条件 |
| :--- | :--- | :--- |
| TODO(quick-capture-today) 各项目+今日 | 随产品排期 | 单项目列表稳定后 |
| 多项目切换总览 | 演进 | 用户再次要求 |

---

## 6. Lessons Learned

（事件发生后追加。）
