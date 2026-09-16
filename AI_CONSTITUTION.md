# AI Collaboration Constitution

This document is the supreme law for all AI-assisted development. Every modification must comply. Violations are errors, not suggestions.

**Treat this as permanent infrastructure, not a guideline.**

---

## Core Principles

### The Constitution Is Not Optional

Any AI action — writing code, modifying docs, creating files, issuing tool commands — must pass this test:

> **“Does this comply with every applicable article of AI_CONSTITUTION.md?”**

If the answer is uncertain, stop. Do not proceed until certainty is achieved.

---

## The Ten Articles

### Article 1. Documentation and Code Are Co-Maintained

- **Rule**: Code and documentation are equally important. They must be maintained together — one never drifts from the other.
- **Interpretation**:
  - Documentation-only changes are normal and valid (design analysis, architecture research, spec writing).
  - Code changes MUST be accompanied by documentation that explains the why — the root cause or design rationale.
  - Every git commit affecting production behavior must include a clear explanation: was the original design wrong? was the code wrong? was testing insufficient?
  - A hotfix without a root cause explanation is a violation — even under time pressure, the explanation must be written.
- **Required in every code-change commit message**:
  - `why: <design flaw / implementation bug / test gap>`
  - `what: <what changed>`
  - Before committing, answer: *design wrong? code wrong? test wrong?* — all three are valid answers.
- **Prohibited**:
  - `"fix bug"` without explaining why the bug existed.
  - `"hotfix"` without a post-fix root cause analysis committed alongside.
  - Documentation that describes the current state without explaining how it got there.
  - Updating a doc to paper over a known inconsistency instead of fixing it first.
- **Enforcement**: Git `commit-msg` hook rejects commits missing `why:` and `what:` blocks or containing generic uninformative placeholders.

---

### Article 2. Root Cause Analysis for Every Change

- **Rule**: Every modification must explain the problem's root cause, impact radius, and expected outcome. When fixing defects, never merely describe symptoms or restate user-observed errors.
- **Why**: Eliminates symptom-driven patching. Without root cause diagnosis, changes add arbitrary conditions or bypass failures, guaranteeing that identical failure classes will recur.
- **Prohibited**:
  - Fixing errors by catching exceptions without diagnosing the trigger.
  - Adding ad-hoc null checks at call sites without identifying why the upstream state is invalid.
- **Correct**:
  - Trace state lifecycle back to invalid mutation source ➔ fix schema, lifecycle, or protocol directly at the source.
- **Enforcement**: Code reviews and post-mortem auditing require every bug ticket and commit to document the 5 Whys.

---

### Article 3. Design Decisions Are Contracts

- **Rule**: Confirmed design decisions — including module boundaries, interface signatures, data constraints, error-handling conventions, and architectural trade-offs — are binding contracts for subsequent implementation.
- **Why**: Working memory degrades across sessions. Confirmed designs prevent AI from reinventing schemas on the fly or silently destroying architectural boundaries.
- **Prohibited**:
  - Silently altering a public signature or database schema during routine bug fixing.
  - Implementing divergent logic because a local requirement seems inconvenient under the approved contract.
- **Correct**:
  - Explicitly propose a contract modification ➔ obtain user approval ➔ record ADR/SPEC amendment ➔ implement code.
- **Enforcement**: Automated API diff checks and contract tests fail upon unannounced breaking signature drifts.

---

### Article 4. No "Later" — Do It Now or Explicitly Defer

- **Rule**: Work within the current task scope must be completed now. Unfinished items cannot be dismissed with vague promises; they must be explicitly tracked with owner, rationale, and trigger conditions.
- **Why**: Unrecorded deferrals accumulate silently into ghost debt, unmaintained TODOs, and phantom bugs that later developers inherit at exponentially higher cost.
- **Prohibited**:
  - Leaving raw `// TODO: fix later` comments without expiration dates or owners.
  - Postponing tests, docs, or index regenerations to "future chores".
- **Correct**:
  - Resolve immediately, OR create a formal structured TODO with deadline and issue link: `TODO(scope): [YYYY-MM-DD] explanation`.
- **Enforcement**: Pre-commit debt linter fails on expired or unformatted TODO comments.

---

### Article 5. Naming Reflects Identity, Not History

- **Rule**: Names of files, directories, modules, interfaces, variables, and configs must express current responsibility and domain identity, not historical migration artifacts.
- **Why**: Temporal names confuse future AI and engineers regarding authoritative purpose, inducing further duplicate abstractions and divergent copies.
- **Prohibited**:
  - Prefixing or suffixing names with `new`, `old`, `temp`, `final`, `v2`, `enhanced`, `wrapper`.
  - Leaving deprecated artifacts in place with active-sounding names.
- **Correct**:
  - Name abstractions after their actual domain role. When superseding, rename the legacy component or retire it completely.
- **Enforcement**: Pre-commit grep rule blocking banned temporal tokens (`new/old/temp/v2`) in new declarations.

---

### Article 6. Configuration Has a Single Source of Truth

- **Rule**: Every configuration value, environment constant, and business rule must possess exactly ONE authoritative declaration. Secondary locations may only derive, generate, or synchronize from it.
- **Why**: Multiple configuration sources cause configuration drift, production-vs-local divergence, and impossible-to-reproduce edge cases.
- **Prohibited**:
  - Hardcoding identical port numbers, URLs, or feature flags across multiple disjoint files.
  - Creating independent configuration lookup tables instead of importing authoritative definitions.
- **Correct**:
  - Define in canonical config/schema file ➔ reference directly or generate secondary configs via automated build scripts.
- **Enforcement**: Pre-commit synchronizer verifying that slave configurations match upstream canonical files.

---

### Article 7. Reserved for Project-Specific Directives

- **Rule**: Reserved for domain-specific, compliance, or runtime constraints specific to active product deployments.
- **Interpretation**: Universal engineering rules are governed by Articles 1–6 and 8–10. When entering production-specific stacks, Article 7 is populated with hard domain boundaries (e.g., zero-PII logging, offline-first sync).
- **Prohibited**: Inventing fictitious project mandates without factual production grounding.

---

### Article 8. Obsolete Documents Must Redirect

- **Rule**: Any specification, rule, ADR, or analysis document that is deprecated, superseded, or migrated must retain an explicit redirect notice pointing to the active authoritative document.
- **Why**: AI and humans retrieve documentation via keyword search and vector indexing. Un-redirected stale documents cause agents to execute defunct workflows.
- **Prohibited**:
  - Deleting or abandoning documents without tombstones.
  - Allowing obsolete design docs to look like viable implementation guidance.
- **Correct**:
  - Place a prominent warning at the document head:
    `> **Redirect**: This document is superseded by [NEW-DOC](../path/to/doc.md). Do NOT use as implementation basis.`
- **Enforcement**: Documentation index script (`npm run docs:index`) validates all cross-links and flags un-redirected deprecated files.

---

### Article 9. No Branching on Time or Non-Deterministic External State

- **Rule**: Business logic branches MUST NOT depend on the current wall-clock time, system date, hardware environment, random entropy, or uncontrolled external conditions unless explicitly part of the business domain with mockable, testable injection boundaries.
- **Why**: Non-deterministic branches turn automated tests into roulette, prevent consistent debugging, and make verification non-reproducible.
- **Prohibited**:
  - Calling `Date.now()`, `new Date()`, `Math.random()`, or inspecting host OS directly inside core business logic.
  - Artificial delays via `setTimeout()` / `sleep()` as synchronization mechanisms.
  - *Note*: Delays are only acceptable for genuine user-facing animations, never for business logic gates or concurrency coordination.
- **Correct**:
  - Inject deterministic clock/entropy providers through dependency injection or parameter passing.
- **Enforcement**: Linter flags direct access to non-deterministic primitives within domain logic packages.

---

### Article 10. The No-Patchwork Rule

- **Rule**: A modification to an existing document or code module must have coherent intent. It must not be a one-off adjustment that introduces a concept without owning its full lifecycle.
- **Why**: Patchwork modifications create fragmented codebases, shadow copies, and broken abstractions, degrading the system over time.
- **Prohibited**:
  - Adding a rule counter (`R11: xxx`) to a field that was not designed to enumerate rules.
  - Updating a stat or counter in documentation to paper over an inconsistency instead of fixing the root cause.
  - Adding explanatory parentheticals instead of fixing an incomplete or misleading description.
  - Introducing a new lookup map where the same data is already available from an existing authoritative definition (see Article 6).
- **Correct**:
  - **New fact discovered** ➔ Add it to the canonical definition, not to a shadow copy.
  - **Design issue found** ➔ Fix it in the source of truth, not work around it in consumers.
  - **Pattern discovered** ➔ Document it formally in the relevant rules file.
- **Enforcement**: Code review and PR gates reject commits that patch symptoms while leaving underlying schemas incoherent.

---

## Enforcement

When an AI session starts, the AI must read this file and acknowledge:

> **“I have read AI_CONSTITUTION.md and will comply with all articles. I will not proceed with any modification that violates these rules.”**

If a violation is discovered after the fact, it must be corrected immediately before the session continues.

---

## Exceptions

The only exception is explicit written approval from the project owner. Any exception must be documented in the same commit as the violating change, citing the specific article being overridden and the verifiable reason.

---

## Review

This Constitution should be reviewed and updated when:
- A new category of violation or incident is discovered.
- A project-level constraint changes.
- An AI session identifies an architectural gap in these rules.

*Every update to this file is itself subject to all articles.*
