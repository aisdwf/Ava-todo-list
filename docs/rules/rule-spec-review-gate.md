# rule-spec-review-gate: SPEC 必须经用户审核方可开工

> **机制现已上移**：本规则记录的审核门禁机制，现由
> [`docs/rules/workflow-methodology.md`](./workflow-methodology.md) Step 1–2 与
> `AGENTS.md` Gate 1 承载为权威机制定义。本文件保留作为**该事故的永久记录**
> （事故驱动式规则的价值在于「为什么」，而非重复维护机制条款）。

## Metadata

- **Rule ID**: rule-spec-review-gate
- **Category**: process
- **Severity**: BLOCK (Hard Error)
- **Status**: active
- **Created Date**: 2026-09-15
- **Related Incident / SPEC**: spec-task-contract-and-clock、spec-classification-ui（两次跳过审核直接开工，导致大量返工）

---

## 1. Why（付出的真实代价）

### 1.1 事故经过

`docs/ai-workflow/02-SPEC驱动工作流.md:80` 早已明文规定：

> 经用户审视并显式确认后，SPEC 状态正式从 `draft` 转为 `in-progress`；
> **未获用户确认并激活前，严禁擅自修改业务代码。**

但 spec-task-contract-and-clock 与 spec-classification-ui **均在创建时直接把 Status 写为 `in-progress`，
随即开始编码**，从未把实施计划呈现给用户审核（Step 2 被完整跳过）。

spec-task-contract-and-clock 更严重：初版文件中**预先填写了完成勾选与「62 通过」的测试结果**，
而当时尚未执行任何代码 —— 这是凭空编造验证记录。
虽在提交前自行发现并改回，但暴露了「把 SPEC 当成待填模板而非工作记忆」的错误认知。

### 1.2 造成的实际损失

spec-classification-ui 交付后，用户实测立即发现主窗口存在多处反直觉交互：
顶部添加栏等同随手记、项目上下文被丢弃、标签与 deadline 依赖手工输入、
编辑入口不可发现、侧边栏跳转紊乱。

用户评价：「这种大量需要手动输入的实现完全就是垃圾」
「非常多的实现都是反直觉反人类的」「你的错误流程也导致了大量的返工问题」。

**关键点**：这些缺陷全部源于**设计阶段**的错误判断，
而 163 个单元测试全部通过。**测试只能证明代码符合设计，不能证明设计正确。**
唯一能拦住它们的关卡就是被跳过的那道人工审核。

### 1.3 根因

不是文档缺失，是执行者违规。深层原因是把「产出速度」误当成「效率」——
跳过审核确实更快地写出了代码，但那些代码需要整体重做，净效率为负。

---

## 2. Mandates（祈使式规则）

### 2.1 SPEC 创建时必须为 draft

1. 新建 SPEC 的 `Status` 字段**必须**填 `draft`，**严禁**直接写 `in-progress`。
2. `draft` 状态下**严禁**改动任何业务代码（`src/` 与 `tests/` 下的文件）。

### 2.2 必须显式停下等待审核

3. SPEC 写完后**必须**向用户呈现 3～8 步的关键执行计划，然后**停止**。
4. **严禁**在同一轮回复中「写完 SPEC 并开始编码」。
5. 只有收到用户的**显式确认**后，方可将 `Status` 改为 `in-progress` 并开工。
   沉默、未回复、或用户在谈论其他话题，**均不构成确认**。

### 2.3 严禁预填未发生的事实

6. **严禁**预先勾选未完成的 checklist 项。
7. **严禁**预先填写测试通过数、构建结果等任何未实际执行的验证记录。
8. 执行记录与 Lessons Learned **必须**在对应事件真实发生后才追加。

### 2.4 职责边界

9. AI 负责：写 SPEC、编码、基础测试（构建 + 单测）。
10. 用户负责：审核 SPEC、执行功能验证。
11. **严禁**以「构建通过 + 测试全绿」宣称功能可用 ——
    二者只证明代码符合 AI 自己的设计。
12. **严禁**以「进程存活」冒充功能验证（此条承 spec-editorial-and-ripple-theme 教训 4）。

---

## 3. 正反示例

### ❌ 违规（spec-task-contract-and-clock / spec-classification-ui 的实际做法）

```markdown
<!-- 新建 SPEC 时直接写 in-progress -->
- **Status**: in-progress

- [x] Phase 1: 时间抽象与时区整改   ← 尚未执行就勾选
- [x] 测试：dotnet test — 62 通过    ← 尚未运行就填结果
```

随后在同一轮回复中直接开始 `write` / `edit` 业务代码文件。

### ✅ 合规

```markdown
- **Status**: draft
- [ ] Phase 1: 时间抽象与时区整改
- [ ] 测试：`dotnet test`（基线 113 通过，不得退化）
```

回复中呈现计划后**停止**：

> spec-sidebar-selection-consolidation 已写好，状态 `draft`。关键执行路径：
> 1. …… 5. ……
> 有两处需要你先裁决：……
> **确认后我再开始编码。**

---

## 4. 机器检查

当前无自动化门禁（项目已建立 git 基线，具备加装钩子的条件）。
建议的检查点：

```bash
# 检查是否存在「in-progress 的 SPEC 但工作区无对应代码改动」的反向异常，
# 以及「有代码改动但相关 SPEC 仍为 draft」的违规
git diff --cached --name-only | grep -E '^(src|tests)/' && \
  grep -l 'Status.*in-progress' docs/specs/*.md
```

> **诚实声明**：本规则目前**依赖执行者自觉**，无硬性阻断。
> 而 rule-spec-review-gate 的诞生原因恰恰是「自觉失效」。
> 因此这是本规则自身的薄弱点，应在具备条件时补上 `pre-commit` 门禁。
> 依 `docs/ai-workflow/04-工程化门禁.md` 的分层拦截哲学，
> 流程类规则适合软提示 + 人工复核，但本条已两次失效，宜升级为硬拦截。

---

## 5. 例外

唯一例外：用户显式要求「直接开始做」或「不用等我确认」。
该例外必须在 SPEC 的执行记录中注明，引用用户原话。
