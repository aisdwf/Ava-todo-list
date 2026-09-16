# TS-XXXX: ${ISSUE_TITLE}

<!-- 
Troubleshooting Runbook & Root Cause Analysis (RCA).
Documents incident diagnosis path, root cause, and remediation steps.
-->

## Incident Metadata

- **Issue ID**: TS-${NUMBER}
- **Severity**: P0 (Critical) | P1 (Major) | P2 (Minor)
- **Status**: investigating | mitigated | resolved
- **Date Discovered**: YYYY-MM-DD HH:mm
- **Date Resolved**: YYYY-MM-DD HH:mm

---

## 1. Symptoms & Error Reports

${SYMPTOMS_OBSERVED_BY_USERS_OR_MONITORING}

- **Error Log / Stacktrace**:
```text
${EXACT_ERROR_LOG_OR_STACKTRACE}
```

---

## 2. Root Cause Analysis (5 Whys / 根因分析)

- **Surface Symptom**: ${SURFACE_LEVEL_ISSUE}
- **Direct Cause**: ${DIRECT_CODE_OR_ENVIRONMENT_CAUSE}
- **Root Cause**: ${DEEPER_ARCHITECTURAL_OR_PROCESS_GAP}

---

## 3. Remediation & Verification

- **Immediate Mitigation (止血方案)**:
  - ${MITIGATION_ACTION}
- **Permanent Fix (永久修复)**:
  - PR/Commit: `${COMMIT_HASH}`
- **Verification Evidence**:
  - [ ] Reproduce environment verified
  - [ ] Automated regression test added

---

## 4. Prevention & Armed Rules

- [ ] New Incident Rule created: `docs/rules/RULE-${NUMBER}.md`
- [ ] Automated check added to pre-commit hook
