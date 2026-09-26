# Commit Conventions

English rule card for AI agents. Commit message `Why:` and `What:` fields **must** be English.

**Do not copy `git log` / `git log --oneline` subjects as templates.** Many
recent commits put `Why:` / `What:` on the first line. Those messages are
**non-compliant**. Imitating them is how the log stays polluted. This file
plus the `commit-msg` hook are the authority, not history.

---

## Mandatory structure

Git has two parts. They are **not** interchangeable:

| Part | Where | What belongs there |
| --- | --- | --- |
| **Subject** | First line only | Conventional type + short summary |
| **Body** | After one blank line | `Why:` then `What:` |

```text
<type>: <short summary>

Why: <design wrong | code wrong | test wrong> — <where> — <root cause>
What: <what actually changed>
```

- `<type>` is one of: `feat` `fix` `docs` `chore` `refactor` `test` `style` `perf` `ci` `build`
- Optional scope: `feat(settings): ...` — `fix(tray): ...`
- Subject length: 12–100 characters. No `Why:` or `What:` on this line.
- A **blank line** must separate subject and body. Do not glue them.
- `Why:` / `What:` start their own lines in the body. Write them **once**.
- A commit without root-cause `Why:` is a **violation**.
- `What:` describes the actual change only — not plans, not "fix later".
- Vague messages (`fix`, `update`, `temp`, `hotfix` alone) are **prohibited**.
- Merge commits (`Merge branch '...'`) and `Revert "..."` are exempt.
- `git commit --no-verify` is **forbidden**. If the hook rejects the message, fix the message.

### Attribution categories

| Category | Use when |
| --- | --- |
| design wrong | Contract, boundary, schema, or decision was wrong |
| code wrong | Implementation / logic / edge case was wrong |
| test wrong | Missing or incorrect verification allowed the defect |

Before committing, answer: design wrong? code wrong? test wrong? At least one must be explicit in `Why:`.

---

## Correct example

```text
fix: hide delete on the Default project

Why: code wrong — Default row still exposed delete; ConfirmDelete called
SqliteProjectRepository.DeleteAsync, which throws for DefaultProject.Id.
What: Hide Default delete, no-op Request/Confirm, and add tests that it
does not throw or enter pending state.
```

`git log --oneline` then shows `fix: hide delete on the Default project`.

---

## Forbidden examples (copied from this repo's polluted log)

These are **invalid**. Do not reproduce them.

```text
# WRONG: Why+What stuffed into the git subject (no type, no body)
Why: code wrong — ... What: Send TaskSavedMessage ...

# WRONG: Why used as the subject; type prefix missing
Why: design wrong — settings still split accent, theme, and material.
What: Collapse settings to Appearance, General, and About.

# WRONG: type subject with no body
fix: hide delete on the Default project

# WRONG: no blank line between subject and Why/What
feat: inherit selected project on task creation
Why: design wrong — AddTaskViewModel never received the selected project id.
What: Pass SelectedProject.Id into task creation.
```

---

## Machine enforcement

The versioned hook is `.githooks/commit-msg`. Install it into the shared git
hooks directory (all worktrees) with:

```text
powershell -File scripts/install-git-hooks.ps1
# or: sh scripts/install-git-hooks.sh
```

Fixture tests:

```text
powershell -File scripts/commit-msg-hook/test-commit-msg.ps1
# or: sh scripts/commit-msg-hook/test-commit-msg.sh
```

The hook rejects: `Why:`/`What:` on the subject, subject longer than 100
characters, missing type prefix, missing blank line, missing body `Why:` /
`What:`, missing `design wrong` / `code wrong` / `test wrong`, and vague `What:`.

If `.git/hooks/commit-msg` is missing, run the installer **before** any commit.

---

## One commit = one reviewable change

- Do not bundle unrelated changes (e.g. a bug fix + a rename + a doc restructure) into one commit.
- The longer the commit message needs to be to explain everything in it, the more it should be split.
- Do not commit "just because something changed" — a commit log full of unattributed noise cannot be mined for patterns later.

---

## TEMP_PATCH

Temporary fixes **must** carry:

```text
TEMP_PATCH: <reason>
Removal date: YYYY-MM-DD
Owner: <name>
```

- No temporary fix without a removal date and owner.
- Prefer fixing properly in-scope; use `TEMP_PATCH` only when the owner explicitly accepts deferral.

---

## Gate alignment

- **Do not** commit before owner manual verification (`AGENTS.md` Gate 4).
- **Do not** decide commit timing unilaterally. The owner indicates when to commit, typically after manual verification passes.
- Agent self-checks are necessary but not sufficient.
- Docs-only commits still use `docs: <summary>` on the subject and `Why:` / `What:` in the body.

---

## Related

- Constitution Article 1–2: co-maintain docs/code; root cause for every change
- Incident record: `docs/rules/rule-commit-msg-layout.md`
- Workflow Step 6: `docs/rules/workflow-methodology.md`
- Hook source: `.githooks/commit-msg`
