# spec-commit-msg-layout: Commit subject type vs body Why/What, enforced by hook

## Metadata

- **ID**: spec-commit-msg-layout
- **Type**: simple
- **Status**: done
- **Owner**: aisdwf
- **Created Date**: 2026-09-27
- **Last Updated**: 2026-09-27

## Why

Agents treat recent `git log` as a writing template. This repo's later
commits put `Why:` / `What:` on the git **subject** (sometimes the entire
message on one line, 400–1500 characters). `git log --oneline` became
unreadable, and each new commit copied the last one.

The rule card only showed a `Why:` / `What:` block. It never said the first
line must be `feat|fix|docs: <summary>`. Constitution Article 1 claimed a
`commit-msg` hook existed; it did not. Context-only reminders cannot beat
search-over-history.

## What

- Rewrite `docs/rules/commit-conventions.md`: subject vs body, anti-patterns
  taken from this log, "do not copy git log".
- Versioned `commit-msg` hook at `.githooks/commit-msg` plus installer into
  the shared git hooks dir (all worktrees).
- Fixture tests under `scripts/commit-msg-hook/`.
- Align `AGENTS.md` Gate 5, Constitution Article 1, workflow Step 6, and an
  incident rule `rule-commit-msg-layout.md`.

Install the hook in this worktree immediately so later commits on any
worktree in this clone cannot keep polluting the log.

## Non-goals

- Rewriting historical commit messages (`git rebase` / filter-repo).
- A `pre-commit` lint of code or docs.
- Conventional-commit scopes as a required field.
- Language detection (English remains a human/rule requirement).

## Constraints and decisions

- Constitution Article 1 already requires `Why:` / `What:`; this SPEC adds
  the missing subject line and makes the claimed hook real.
- Shared `main/.git/hooks` (not `core.hooksPath=.githooks`) so every
  worktree is gated as soon as the installer runs, including trees that
  have not merged this branch yet.
- Merge and `Revert "..."` messages stay exempt.
- `--no-verify` is forbidden in agent rules; the hook text repeats that.

## Acceptance criteria

- [x] `commit-conventions.md` shows type-subject + blank line + body Why/What,
      plus forbidden copies of this repo's polluted subjects.
- [x] Hook rejects: Why/What on subject, missing type, missing body Why/What,
      missing attribution category, vague What, subject over 100 chars,
      missing blank line.
- [x] Hook accepts: `fix`/`feat(scope)`/`docs` with Why/What, two Why blocks,
      TEMP_PATCH with removal date and owner, merge subjects.
- [x] `powershell -File scripts/commit-msg-hook/test-commit-msg.ps1` passes.
- [x] `powershell -File scripts/install-git-hooks.ps1` installs into the shared
      hooks dir; a probe `git commit` with a one-line Why: subject is rejected.
- [x] AGENTS.md / constitution / workflow / rules index mention the layout
      and the hook.

## Staged plan

1. Rule card + incident record + SPEC index.
2. Hook, fixtures, installer.
3. Align AGENTS.md, constitution, workflow.
4. Install into shared `.git/hooks` and run fixture tests.

## Change checklist

- [x] `docs/rules/commit-conventions.md`
- [x] `.githooks/commit-msg`
- [x] `scripts/install-git-hooks.ps1` / `.sh`
- [x] `scripts/commit-msg-hook/` fixtures and runner
- [x] `docs/rules/rule-commit-msg-layout.md`
- [x] `AGENTS.md` Gate 5 + commands
- [x] `AI_CONSTITUTION.md` Article 1
- [x] `docs/rules/workflow-methodology.md` Step 6
- [x] `docs/rules/README.md` and `docs/README.md` indexes
- [x] `docs/specs/README.md` index + handoff
- [x] Installer run on this clone

## Progress log

### 2026-09-27

- Completed: Owner confirmed the diagnosis (subject vs body) and asked for
  a hook so history-copy cannot keep polluting the log. Worktree
  `feature/commit-msg-hook` created from `dev`. Rule card, incident record,
  hook, fixtures, installer, and doc alignment are in the tree. Shared
  `main/.git/hooks/commit-msg` is installed. Fixture tests passed. A live
  `git commit --allow-empty` with a Why:/What: subject was REJECTED and
  did not move HEAD.
- Decisions: Shared `.git/hooks` install (all worktrees). No history rewrite.
  Document `powershell -File` (this clone has Windows PowerShell 5, not pwsh).
- Current resume point: owner said 没问题 / 合并 (2026-09-27). SPEC closed
  `[DONE]` on this branch before merge into `dev`.

## Verification

- Automated: `sh scripts/commit-msg-hook/test-commit-msg.sh`; installer path
  check; no FlowTask code change so `dotnet test` is regression-only.
- Manual: owner accepted the layout and hook (没问题，合并). The next real
  commit on this branch must itself use a `feat|fix|docs:` subject.
- Not run or not covered: rewriting old SHAs.

## Risks and open questions

- Owner: after merge, other clones must run the installer once (hook is not
  in git's `.git/` on a fresh clone).
- Blocker or trigger: none for this clone once installer runs.

## Lessons learned

Rule cards that only show `Why:` / `What:` without "first line is the git
subject" get copied into `git commit -m "..."` as a single line. Agents then
imitate `git log --oneline`. A hook is the only gate that beats that loop.

## Related documents

- SPECs: none prior
- ADRs: none
- Rules: `commit-conventions.md`, `rule-commit-msg-layout.md`
- Analysis: none
