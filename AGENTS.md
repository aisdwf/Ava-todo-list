# AGENTS.md

English entry for AI agents. This file is a **pointer and hard-gate index**, not a complete operating manual.

**Do not act on this file alone.** Before any modification, satisfy Gate 2 (mandatory reads).

---

## Hard Gates (non-negotiable)

### Gate 1 — Confirmation before code

- **Do not** modify any code file before explicit owner confirmation.
- Before any code change: restate the task in one paragraph, then **wait** for explicit owner confirmation.
- For complex tasks: additionally produce a SPEC marked `[DRAFT]` plus a 3–8 step plan, then **wait** for explicit owner confirmation.
- Writing a SPEC or plan is **not** confirmation. No code file may be modified until the owner explicitly confirms.
- Silence, no reply, or the owner discussing something else does **not** count as confirmation.

### Gate 2 — Mandatory docs before acting

Before acting, read:

1. `docs/rules/workflow-methodology.md`
2. `AI_CONSTITUTION.md`
3. `docs/rules/docs-conventions.md` and `docs/rules/commit-conventions.md`
4. `docs/rules/project-rules.md`, `docs/rules/technical-rules.md`, and the relevant incident-driven cards under `docs/rules/rule-*.md` (including `rule-spec-on-dev-before-merge.md` before any merge into `dev`)

This entry file alone is **not** sufficient to act on.

### Gate 3 — Execution rhythm (visible progress)

- After every work unit (doc read / file edit / check run), output **one status line** (what was done + outcome).
- Before starting, declare **files-to-touch** and **step count**.
- Keep a **visible todo list** updated live; **never** batch status updates.
- Answer `progress?` at any time against the current todo list.

### Gate 4 — Commit only after owner verification

- **Do not** commit before the owner's manual verification.
- Agent self-checks (build + unit tests) are necessary but **not** sufficient to claim a feature works.
- The owner decides commit timing; do not decide unilaterally that "this phase is ready to commit."

### Gate 5 — Root cause in every code commit

- Every code commit **must** contain English `Why:` (design wrong / code wrong / test wrong) and `What:`.
- One commit = one independently-reviewable change. Do not bundle unrelated changes.
- See `docs/rules/commit-conventions.md`.

---

## Commands

| Purpose | Command |
| --- | --- |
| Build | `export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"; export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1; dotnet build FlowTask.sln -v q --nologo` |
| Test | `dotnet test FlowTask.sln --nologo -v q` |
| Run (manual verification only, not a functional check) | `./run.sh` |
| Docs index | *(none — `docs/specs/README.md` is hand-maintained; see `docs/rules/docs-conventions.md`)* |

Baseline: build 0 warnings / 0 errors; tests 163 passing. Do not regress below this baseline.

---

## Documentation index

| Document | Role |
| --- | --- |
| [AI_CONSTITUTION.md](./AI_CONSTITUTION.md) | Supreme non-negotiable principles |
| [docs/rules/workflow-methodology.md](./docs/rules/workflow-methodology.md) | Task types, complex Steps 0–6, execution rhythm, error handling |
| [docs/rules/docs-conventions.md](./docs/rules/docs-conventions.md) | SPEC location (area folders), naming, status tags, freshness |
| [docs/rules/commit-conventions.md](./docs/rules/commit-conventions.md) | Mandatory `Why:` / `What:` and TEMP_PATCH |
| [docs/rules/project-rules.md](./docs/rules/project-rules.md) | FlowTask-specific business/technical/architecture rules |
| [docs/rules/technical-rules.md](./docs/rules/technical-rules.md) | Mandatory technical/architecture decomposition rules (e.g. ViewModel command decomposition) |
| [docs/rules/rule-spec-review-gate.md](./docs/rules/rule-spec-review-gate.md) | Incident record: SPEC review gate was skipped twice, causing full rework |
| [docs/rules/rule-spec-on-dev-before-merge.md](./docs/rules/rule-spec-on-dev-before-merge.md) | Incident record: SPEC must be closed on `dev` before merging a feature branch |
| [docs/rules/rule-no-invented-user-behavior.md](./docs/rules/rule-no-invented-user-behavior.md) | Incident record: interaction decisions invented without user evidence |
| [docs/rules/rule-doc-boundary.md](./docs/rules/rule-doc-boundary.md) | design vs spec type boundary and naming rules |
| [docs/rules/rule-code-standards.md](./docs/rules/rule-code-standards.md) | C# 12 / Avalonia 11 coding and comment standards |
| [docs/specs/README.md](./docs/specs/README.md) | SPEC index, area folders, and session handoff entry point |
| [docs/ai-workflow/](./docs/ai-workflow/) | Deep methodology rationale (private, gitignored; not required to operate the repo) |

---

## Language policy (for agents)

- **English required**: this file, all `docs/rules/*.md` rule cards, and commit `Why:` / `What:` fields.
- **No language mandate** for SPEC body narrative, ADRs, requirements, design docs, or human methodology prose (may be Chinese).
