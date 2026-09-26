# Workflow Methodology

English rule card for AI agents. Human-facing deep methodology lives under `docs/ai-workflow/` (private, gitignored) and does not replace this file.

---

## Mandatory pre-read

Before acting on any task, read:

1. This file
2. `AI_CONSTITUTION.md`
3. Relevant peers under `docs/rules/*.md` (at minimum `docs-conventions.md`, `commit-conventions.md`, and `rule-doc-boundary.md` for any doc or code work)

`AGENTS.md` alone is **not** sufficient.

---

## Task types

Classify every task before work starts. Classification decides SPEC depth and path.

| Type | When | Path |
| --- | --- | --- |
| `simple` | Scope clear, local impact, known path, direct verification | Restate briefly → owner confirmation (Gate 1) → implement → verify → record outcome |
| `complex` | Multi-module, architecture boundary, data flow, migration, multi-role, or high regression risk | Full Steps 0–6 below |
| `exploratory` | Goal or approach unsettled; research / experiment / comparison needed | Define question + hypotheses → research → conclusions; **do not** treat experiment code as delivery unless explicitly promoted |

**Do not** disguise a `complex` task as `simple`.

---

## Complex path — Steps 0–6

### Step 0 — Restatement → wait

- Restate the task in one paragraph in your own words (goals, boundaries, assumptions, open questions).
- **Wait** for explicit owner confirmation.
- Rejected restatement → re-restate and wait again. **Do not** proceed.

### Step 1 — SPEC `[DRAFT]`

- Write the SPEC on the `dev` worktree. Feature and bugfix branches do not own SPEC files.
- `docs/rules/docs-conventions.md` is the single authority for SPEC location, filename, body format, lifecycle tags, transitions, and stale thresholds. Do not redefine them here or in task-local instructions.
- Search `docs/specs/<area>/` for an existing `[IN-PROGRESS]` or `[DRAFT]` SPEC covering the same area.
- **Prefer contributing** to that SPEC; do not fork a parallel working memory.
- If none exists, create `docs/specs/<area>/spec-<feature>[DRAFT].md` (lifecycle tag **required** in the filename; group by feature/area folder; create a new area only per `docs-conventions.md` rules).
- Follow the canonical metadata, body order, and task-type section matrix in `docs-conventions.md`.
- A `complex` SPEC must include at minimum:
  - **Why / What / Non-goals**
  - **Constraints and decisions**
  - **Acceptance criteria**
  - **Staged plan**
  - **Change checklist**
  - **Progress log with a current resume point**
  - **Verification**
  - **Risks and open questions**
  - **Lessons learned**
  - **Related documents**
- Start as `[DRAFT]` in both header and filename.
- Writing a SPEC is **not** permission to edit code.
- Research reports and durable exploratory findings belong in `docs/analysis/`; link them from the SPEC.

### Step 2 — Plan → wait → `[IN-PROGRESS]`

- Present a **3–8 step** execution plan to the owner.
- **Wait** for explicit confirmation. Silence, no reply, or the owner discussing something else does **not** count as confirmation.
- Only then flip status to `[IN-PROGRESS]` **and rename** the file to `spec-<feature>[IN-PROGRESS].md`.
- **Iron rule:** once implementation starts, status must not remain `[DRAFT]`.
- Update `docs/specs/README.md` in the same change (hand-maintained index; see `docs-conventions.md`).

### Step 3 — Staged execution

- Execute phase by phase.
- After each phase / work unit, update the SPEC checklist and progress notes so a fresh session can resume by re-reading the SPEC alone.
- Record subagent dispatches (type, task id, outcome, harvested conclusions) back into the SPEC when used.
- If an `[IN-PROGRESS]` SPEC is stale under `docs-conventions.md`, record an owner decision before resuming implementation.

### Step 4 — Verification

- Run project checks from `AGENTS.md` Commands (do not invent commands).
- Fix failures immediately; **do not** proceed past a red check.
- Provide the owner a clear verification entry for manual review.
- Passing build + tests proves the code matches the SPEC's own design. It does **not** prove the feature is usable — that is the owner's manual verification, not the agent's.

### Step 5 — Docs review → `[DONE]` (on `dev` only)

- SPEC create / rename / status / `docs/specs/README.md` happen on the `dev`
  worktree. Do not close a SPEC on a feature or bugfix branch.
- Sync docs with code (constitution: co-maintain).
- Redirect superseded docs.
- When acceptance and checklist are complete, set header status to `[DONE]` (or the appropriate terminal tag) **and rename** to `spec-<feature>[DONE].md` (or matching terminal tag).
- Update `docs/specs/README.md` in the same change.
- See `docs/rules/rule-spec-on-dev-before-merge.md`.

### Step 6 — Commit only after owner verification; merge after SPEC is closed

- **Do not** commit until the owner has manually verified.
- Agent self-checks are necessary but **not** sufficient.
- The owner decides commit timing; do not decide unilaterally that "this phase is ready to commit."
- Code commits must follow `commit-conventions.md` (`Why:` / `What:` in English).
- One commit = one independently-reviewable change. Do not bundle unrelated changes into a single commit.
- Close the SPEC on `dev` **before** merging the feature/bugfix branch into `dev`.
  Do not merge a stale SPEC index from a branch forked off `main`.

---

## Execution Rhythm (Visible Progress) — hard rules

These are **hard rules**, not style preferences:

1. After every work unit (doc read / file edit / check run), output **one status line** (what was done + outcome).
2. Before starting, declare **files-to-touch** and **step count**.
3. Keep a **visible todo list** updated live.
4. **Never** batch status updates.
5. Answer `progress?` at any time against the current todo list.
6. Record blockers and phase outcomes back into the SPEC when one exists.

---

## Error handling

| Situation | Required action |
| --- | --- |
| Owner rejects restatement | Re-restate; wait again; **do not** edit code |
| Blocker (missing info, dependency, ambiguity) | Record in SPEC (or status line if no SPEC); ask the owner; **do not** guess past the blocker |
| Check failure (lint / test / typecheck / build) | Fix immediately; re-run the failed check; **do not** proceed to later phases or commit |
| Scope turns out larger mid-flight | Reclassify; if now `complex`, run Step 0–2 again before more code |
| Wrong direction / broken contract | Stop patching; revert to known-good; record lesson in SPEC; realign with owner |

---

## Simple and exploratory notes

- **Simple:** still requires Gate 1 confirmation before code, Gate 2 pre-reads, visible progress, and verification. A minimal SPEC or progress note is enough when the owner does not require a full SPEC.
- **Exploratory:** deliver conclusions first. Durable findings live under `docs/analysis/`; a SPEC may track cross-session research work but must link to the analysis output. Mark experiment artifacts clearly. Promoting experiment code to delivery requires a new confirmation cycle (and usually a `complex` or `simple` implementation SPEC).

---

## Scope discipline

- The owner asks for A; do only A. Do not add backups, extra check scripts, reminders, or long summaries unless asked.
- Finish concisely; report concisely.

---

## Related

- SPEC mechanics: `docs/rules/docs-conventions.md`
- SPEC lifecycle on `dev` before merge: `docs/rules/rule-spec-on-dev-before-merge.md`
- Type boundary (design vs spec): `docs/rules/rule-doc-boundary.md`
- Commit attribution: `docs/rules/commit-conventions.md`
- Human methodology: `docs/ai-workflow/02-SPEC驱动工作流.md` (private, gitignored)
