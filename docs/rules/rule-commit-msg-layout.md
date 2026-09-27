# rule-commit-msg-layout: 第一行是 type 摘要，Why/What 只写在正文

> **机制现已上移**：版式与拦截由
> [`docs/rules/commit-conventions.md`](./commit-conventions.md) 与
> `.githooks/commit-msg` 承载。本文件保留为**该事故的永久记录**。

## Metadata

- **Rule ID**: rule-commit-msg-layout
- **Category**: process
- **Severity**: BLOCK (Hard Error)
- **Status**: active
- **Created Date**: 2026-09-27
- **Related Incident / SPEC**: spec-commit-msg-layout

---

## 1. Why（付出的真实代价）

### 1.1 事故经过

`commit-conventions.md` 只给了 `Why:` / `What:` 模板，没有写明 git **第一行
（subject）** 与 **正文（body）** 的分工。早期提交是对的：

```text
fix: remove non-interactive plus icon from task creation bar

Why: design wrong — ...
What: Remove the ...
```

之后代理把 Why/What 整段塞进 `git commit` 的第一行（有的还把 Why 和 What
糊在同一行）。`git log --oneline` 出现 400～1500 字的 subject。新会话检索
最近提交当范文，于是错误被复制、越堆越多。

宪法 Article 1 写着「`commit-msg` hook 会拒绝缺少 why/what 的提交」——
仓库里当时没有这个 hook。

### 1.2 造成的实际损失

- 历史无法扫：`git log --oneline` / GitHub 提交列表读不成类型与摘要。
- 范文污染：越新的提交越错，上下文约束压不过 `git log` 检索。
- 规范自我打脸：规则卡与真实 log 互相矛盾，代理不知道该信哪边。

### 1.3 根因

不是「没写 Why/What」，是 **写错位置**。Why/What 占用了本该放
`feat` / `fix` / `docs` 的 subject。仅靠 AGENTS.md 提醒拦不住，因为检索
历史比读规则更容易。

---

## 2. Mandates

1. **MUST** 用第一行写 `feat|fix|docs|...: <短摘要>`。
2. **MUST** 空一行后，在正文写且只写一次 `Why:` 与 `What:`。
3. **FORBIDDEN** 把 `Why:` / `What:` 放进第一行，或从 `git log --oneline`
   复制以 `Why:` 开头的 subject。
4. **FORBIDDEN** `git commit --no-verify`。hook 拒绝则改消息，不关钩子。
5. 新克隆 **MUST** 在第一次提交前运行 `scripts/install-git-hooks.ps1`
   （或 `.sh`）。

---

## 3. Concrete Examples

### ❌ Anti-Pattern

```text
Why: code wrong — ... What: Send TaskSavedMessage ...
```

### ✅ Approved Pattern

```text
fix: hide delete on the Default project

Why: code wrong — Default row still exposed delete; ConfirmDelete threw.
What: Hide Default delete and add tests that it does not throw.
```

---

## 4. Automated Enforcement

```bash
powershell -File scripts/commit-msg-hook/test-commit-msg.ps1
powershell -File scripts/install-git-hooks.ps1
```

`git commit` 走共享目录 `main/.git/hooks/commit-msg`（由安装脚本从
`.githooks/commit-msg` 拷入）。缺 type、Why/What 在第一行、缺正文 Why/What
一律拒绝。

---

## 5. Exceptions & Escape Hatch

- Merge（`Merge branch '...'`）与 `Revert "..."` 免检。
- 禁止静默绕过。紧急绕过必须所有者明示，且不得作为后续范文。
