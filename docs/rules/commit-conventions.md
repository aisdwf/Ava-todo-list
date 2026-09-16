# Commit Conventions

English rule card for AI agents. Commit message `Why:` and `What:` fields **must** be English.

---

## Mandatory structure

Every **code** commit must contain:

```text
Why: <design wrong | code wrong | test wrong> — <where> — <root cause>
What: <what actually changed>
```

- A commit without root-cause `Why:` is a **violation**.
- `What:` describes the actual change only — not plans, not "fix later".
- Vague messages (`fix`, `update`, `temp`, `hotfix` alone) are **prohibited**.

### Attribution categories

| Category | Use when |
| --- | --- |
| design wrong | Contract, boundary, schema, or decision was wrong |
| code wrong | Implementation / logic / edge case was wrong |
| test wrong | Missing or incorrect verification allowed the defect |

Before committing, answer: design wrong? code wrong? test wrong? At least one must be explicit in `Why:`.

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
- Docs-only commits should still be clear; code commits **must** satisfy `Why:` / `What:`.

---

## Related

- Constitution Article 1–2: co-maintain docs/code; root cause for every change
- Workflow Step 6: `docs/rules/workflow-methodology.md`
