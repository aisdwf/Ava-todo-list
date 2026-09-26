# Documentation Conventions

English rule card for AI agents. Applies to SPECs and rule docs under this repository.

---

## Authority and scope

This file is the **single authoritative definition** for:

- SPEC location and area folders;
- SPEC filename format;
- required metadata and body sections;
- lifecycle status tags and transitions;
- stale-SPEC handling;
- generated SPEC index behavior.

Other files may explain the workflow, but **must link to this file instead of redefining these rules**. If another document conflicts with this file, this file wins.

`docs/rules/rule-doc-boundary.md` remains authoritative for the **design vs spec** type boundary (what belongs in `docs/design/` versus `docs/specs/`). This file governs SPEC mechanics only.

---

## SPEC purpose

A SPEC is task working memory. It must let a fresh AI session recover:

- why the task exists;
- what is and is not in scope;
- which contracts and decisions already bind the work;
- what has been completed;
- what remains;
- how completion will be verified.

A SPEC is not a dumping ground for unrelated research, meeting notes, or permanent reference material.

---

## Location and area folders

- Root: `docs/specs/`
- Required layout: `docs/specs/<area>/spec-<feature>[STATUS].md`
- `<area>` must be a stable domain, product capability, or feature family.
- `<area>` must describe identity, not lifecycle, owner, date, task type, or implementation technology.
- Place a new SPEC in the **most specific existing area** that owns the behavior.

Current areas in this project:

```text
docs/specs/infrastructure/    # MVVM/persistence/app skeleton
docs/specs/visual-theme/      # visual language, appearance, theming
docs/specs/task-domain/       # task/project/tag data contract and clock
docs/specs/main-window/       # main window layout and sidebar/selection
docs/specs/quick-capture/      # quick-capture window and hotkey capture
docs/specs/docs-system/       # documentation system restructuring itself
```

Invalid area examples:

```text
docs/specs/draft/          # status folder
docs/specs/2026-09/        # date folder
docs/specs/aisdwf/         # owner folder
docs/specs/complex/        # task-type folder
```

### Creating a new area

Create a new `<area>` only when:

1. no existing area has clear ownership;
2. the new area represents a stable domain identity;
3. its boundary can be described in one sentence;
4. it will not create a second home for the same feature family.

Register the new area in this file's area list in the same change that creates the folder.

### Research and analysis placement

Research reports, comparisons, gap analyses, and investigation results belong under `docs/analysis/`, not in an `analysis` status folder under `docs/specs/`.

An `exploratory` task may use a SPEC as cross-session working memory, but its durable findings must be published under `docs/analysis/` and linked from the SPEC.

**Exception — `docs/technical/`**: research that is not tied to a specific SPEC or decision snapshot, and is expected to be referenced repeatedly or updated as a standing technical resource (e.g. architecture reference surveys, open-source case studies used to derive a `docs/rules/technical-rules.md` mandate), belongs under `docs/technical/` instead of `docs/analysis/`. See `docs/technical/README.md` for the boundary. If a `docs/technical/` finding is promoted into a mandatory rule, the rule card states the normative requirement; it does not duplicate the research narrative.

---

## Search before creation

Before creating a SPEC:

1. search `docs/specs/<area>/` by feature name, domain terms, and affected contracts;
2. inspect active `[DRAFT]` and `[IN-PROGRESS]` SPECs in the matching area;
3. prefer updating the existing working-memory SPEC;
4. do not create parallel SPECs for the same task.

One task should have one active working-memory SPEC. If ownership genuinely splits, each SPEC must declare its boundary and cross-link the others.

---

## Naming: lifecycle in the filename

- Pattern: `spec-<feature-description>[STATUS].md`
- `<feature-description>` is lowercase, hyphen-separated, and describes identity, not history.
- `[STATUS]` must be exactly one tag from the authoritative table below, written in **uppercase**, wrapped in square brackets, immediately before `.md`.
- The filename carries lifecycle state. Header `Status` (lowercase) and filename `[STATUS]` (uppercase) must always refer to the same state.
- On every status change, rename the file in the same change.
- Do not use `new`, `old`, `temp`, `final`, `v2`, owner names, or dates as feature identity.
- No incrementing numeric IDs anywhere in the filename (see `rule-doc-boundary.md` §3.1).

Valid:

- `spec-quick-window-hotkey-capture[IN-PROGRESS].md`
- `spec-tag-entity[DONE].md`
- `spec-fluent-ui[SUPERSEDED].md`

Invalid:

- `spec-quick-window-hotkey-capture.md` — missing status
- `spec-quick-window-hotkey-capture-draft.md` — non-canonical status syntax
- `draft-spec-quick-window-hotkey-capture.md` — status in the wrong position
- `spec-tag-entity-v2[DRAFT].md` — historical identity

---

## Required header metadata

Every SPEC must open with:

```markdown
# spec-<feature>: <Feature or Task Name>

## Metadata

- **ID**: spec-<feature>
- **Type**: simple | complex | exploratory
- **Status**: draft | in-progress | done | done-refactored | superseded | archived | obsolete
- **Owner**: <owner or creating agent identity>
- **Created Date**: YYYY-MM-DD
- **Last Updated**: YYYY-MM-DD
```

Rules:

- `Status` (lowercase, hyphenated: `in-progress`) must correspond to the filename `[STATUS]` (uppercase: `IN-PROGRESS`).
- `ID` must match the filename's feature-description segment.
- `Last Updated` changes only when meaningful progress, decisions, risks, or verification results are recorded. Touch-only date updates are prohibited.
- `Owner` identifies responsibility; it is not used as a directory name.

---

## Canonical SPEC body

Use the following order so both humans and AI can recover task state predictably:

```markdown
## Why

Problem, root cause or motivation, impact, and why the work is needed now.

## What

Scope, affected modules, contracts, data flows, and expected outcome.

## Non-goals

Explicitly excluded work.

## Constraints and decisions

Applicable constitution articles, rules, ADRs, accepted contracts, and assumptions.

## Acceptance criteria

- [ ] Observable, verifiable completion condition.

## Staged plan

1. Independently verifiable phase.

## Change checklist

- [ ] Concrete file, function, migration, contract, or documentation item.

## Progress log

### YYYY-MM-DD

- Completed:
- Decisions:
- Current resume point:
- Subagent/task references, when used:

## Verification

- Automated:
- Manual:
- Not run or not covered:

## Risks and open questions

- Owner:
- Blocker or trigger:

## Lessons learned

Root causes, failed paths, and candidates for permanent rules.

## Related documents

- SPECs:
- ADRs:
- Rules:
- Analysis:
```

### Required sections by task type

| Section | `simple` | `complex` | `exploratory` |
| --- | --- | --- | --- |
| Why / What | Required when a SPEC exists | Required | Required as question and motivation |
| Non-goals | Recommended | Required | Required |
| Constraints and decisions | Required when applicable | Required | Required assumptions |
| Acceptance criteria | Required | Required | Required decision / evidence criteria |
| Staged plan | Optional | Required | Required research plan |
| Change checklist | Required for implementation | Required | Optional unless producing deliverables |
| Progress log | Recommended | Required | Required for cross-session work |
| Verification | Required | Required | Required evidence and reproducibility |
| Risks and open questions | Recommended | Required | Required |
| Lessons learned | Recommended | Required at closure | Required conclusions |
| Related documents | Required when links exist | Required | Required; durable findings link to `docs/analysis/` |

Do not add empty ceremonial sections merely to satisfy shape. If a required section has no content, state `None` and explain why.

---

## Authoritative status tags

Use exactly these tags. Filename uses the uppercase bracketed form; header `Status` uses the lowercase hyphenated form. No aliases or additional tags are valid.

| Filename tag | Header value | Meaning | When to use |
| --- | --- | --- | --- |
| `[DRAFT]` | `draft` | Proposed working memory; not approved for implementation | Initial state of every new implementation SPEC |
| `[IN-PROGRESS]` | `in-progress` | Owner-confirmed work is underway | Immediately before implementation starts |
| `[DONE]` | `done` | Acceptance, verification, and documentation are complete | Work is implemented and manually verified |
| `[DONE-REFACTORED]` | `done-refactored` | Completed behavior remains valid after a later refactor/governance pass | The original capability exists but its implementation evolved |
| `[SUPERSEDED]` | `superseded` | Replaced by another SPEC | A newer SPEC is the active authority |
| `[ARCHIVED]` | `archived` | Historical, research, or analysis pointer; non-executable | The record is retained for context, not implementation |
| `[OBSOLETE]` | `obsolete` | Cancelled or no longer applicable | The proposal was abandoned or invalidated |

`[BLOCKED]` is intentionally **not** a lifecycle tag. A blocked task remains `[IN-PROGRESS]` and records its blocker, owner, and resume condition under `Risks and open questions`.

### Allowed lifecycle

```text
[DRAFT] -> [IN-PROGRESS] -> [DONE] -> [DONE-REFACTORED]
    |            |             |
    +------------+-------------+-> [SUPERSEDED]
    +------------+----------------> [ARCHIVED]
    +------------+----------------> [OBSOLETE]
```

Rules:

- Implementation must never start while the SPEC remains `[DRAFT]`.
- `[DONE]` requires completed acceptance criteria, verification evidence, synchronized documentation, and owner manual verification.
- `[DONE-REFACTORED]` does not replace a new refactor SPEC; it marks the historical implementation SPEC after the refactor is complete and linked.
- `[SUPERSEDED]` must identify the replacement.
- `[ARCHIVED]` is non-executable.
- `[OBSOLETE]` must state why the work is no longer applicable.
- Status changes must reflect reality; status-only churn is prohibited.

### Status change procedure

1. Update header `Status`.
2. Rename the file so the `[STATUS]` suffix matches.
3. Update `Last Updated` with the applicable current date.
4. Add any required redirect or reason.
5. Update every relative-path reference to the renamed file across `docs/` and code comments in the same change.
6. Update `docs/specs/README.md` in the same change (see Index convention).
7. Commit the rename, metadata change, redirect, and reference/index updates together.

---

## Redirect and terminal-state requirements

`[SUPERSEDED]` must begin its body with:

```markdown
> **Redirect**: Superseded by [spec-replacement[STATUS]](../area/spec-replacement[STATUS].md). Do NOT implement from this file.
```

`[ARCHIVED]` must state:

- that it is non-executable;
- why it is retained;
- which `docs/analysis/` report or active SPEC contains current guidance, when one exists.

`[OBSOLETE]` must state:

- why the proposal was abandoned;
- whether any part was implemented;
- which active document should be used instead, when one exists.

Terminal documents are retained for traceability. Do not silently delete them.

---

## Freshness and anti-accumulation rules

- `[IN-PROGRESS]` with no meaningful update for more than 14 days must be flagged for owner review.
- `[DRAFT]` with no meaningful update for more than 30 days must be flagged for activation, rewrite, archiving, or obsolescence.
- A stale warning must not automatically change status.
- A stale SPEC must record an owner decision before further implementation.
- Repeatedly updating only `Last Updated` to suppress a stale warning is prohibited.
- Closed work must not remain `[IN-PROGRESS]`.
- Cancelled work must not remain `[DRAFT]`.
- Duplicate SPECs must be merged or explicitly superseded.
- Unowned open questions and TODOs are prohibited.

These thresholds (14 / 30 days) are this project's canonical values. Changing them requires updating this file and any checker in the same change.

---

## Index convention (`docs/specs/README.md`)

- No automated doc-index generator exists for this .NET project (see `AGENTS.md` Commands table).
- `docs/specs/README.md` is therefore **hand-maintained**, not machine-generated.
- Any SPEC add, rename, area move, or status change **must** update `docs/specs/README.md` in the same change: the status-tag legend, the area/index table, and the "handoff entry point" section.
- Those SPEC and index edits happen on `dev`. Do not close or reindex a SPEC on a feature/bugfix branch and then merge it (see `rule-spec-on-dev-before-merge.md`).
- Do not let the index drift from the physical files. A stale index is treated the same as a stale SPEC: report it, do not silently leave it.
- If a docs-index command is added later, this section must be rewritten to point to it and hand-editing must stop.

---

## Machine-checkable rules

When project tooling is added, SPEC checks should validate:

- location matches `docs/specs/<area>/`;
- filename matches `spec-<feature>[STATUS].md`;
- filename and header status match;
- status is one of the authoritative tags;
- required metadata and task-type sections exist;
- `[IN-PROGRESS]` contains a staged plan, checklist, progress log, and resume point;
- terminal states contain required redirects or reasons;
- stale thresholds are reported;
- `docs/specs/README.md` matches physical files.

Until tooling exists, `rule-doc-boundary.md` §4 provides the manually-run `grep`/`find` checks that cover naming and type-boundary violations. Run them before every commit that touches `docs/`.

---

## Related

- Workflow path: `docs/rules/workflow-methodology.md`
- Type boundary (design vs spec): `docs/rules/rule-doc-boundary.md`
- Commit attribution: `docs/rules/commit-conventions.md`
- Human methodology: `docs/ai-workflow/02-SPEC驱动工作流.md` (private, gitignored)
