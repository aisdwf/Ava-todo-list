# RULE-XXXX: ${RULE_TITLE}

<!-- 
Armed Incident Museum Rule Card.
Rules are not generic styling guides - they are defensive shields crystallized from expensive bugs.
Standards: Actionable (imperative), Accompanied by real cases, Explaining Why, Non-negotiable phrasing.
-->

## Metadata

- **Rule ID**: RULE-${NUMBER}
- **Category**: process | architecture | incident
- **Severity**: BLOCK (Hard Error) | WARN (Soft Warning)
- **Status**: draft | active | deprecated
- **Created Date**: YYYY-MM-DD
- **Related Incident / SPEC**: SPEC-${XXXX}

---

## 1. Why (The Expensive Lesson)

<!-- 
Explain why this rule exists and the cost paid previously.
Enables AI and engineers to exercise sound judgment within the spirit of the rule.
-->

${PAINFUL_INCIDENT_BACKGROUND_AND_COST_DESCRIPTION}

---

## 2. Mandates (Imperative Rules)

<!-- 
Use strict imperative mood. Never negotiate. ("MUST", "FORBIDDEN", "SHALL NOT").
-->

1. Developers/AI **MUST** ${MANDATORY_BEHAVIOR_1}.
2. **FORBIDDEN** to ${PROHIBITED_BEHAVIOR_2}.

---

## 3. Concrete Examples (真实案例)

### ❌ Anti-Pattern (The Mistake)

```typescript
// Anti-pattern: reproducing the exact historical incident
${CONCRETE_BAD_EXAMPLE_CODE}
```

### ✅ Approved Pattern (The Fix)

```typescript
// Approved pattern: compliant and robust implementation
${CONCRETE_GOOD_EXAMPLE_CODE}
```

---

## 4. Automated Enforcement (机器检查命令)

<!-- 
A rule without machine verification is useless.
Provide a deterministic command (grep, AST query, linter script) to catch violations.
-->

```bash
# Must return 0 when clean, non-zero when violated
${COMMAND_OR_GREP_SCRIPT_TO_DETECT_VIOLATION}
```

---

## 5. Exceptions & Escape Hatch

- Under no circumstance may this rule be bypassed silently.
- Emergency bypass requires explicit human approval with documented rationale: `--no-verify`.
