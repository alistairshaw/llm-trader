---
schema_version: 1
id: S7-012
title: Build authorized kill-switch controls
stage: 7
status: done
priority: 830
type: feature
depends_on: [S7-003, S7-005, S7-007]
labels: [wpf, kill-switch, safety]
created: 2026-08-22
updated: 2026-08-22
---
# S7-012: Build Authorized Kill-Switch Controls

## Objective
Expose accessible, confirmed, authorized controls for every kill-switch scope.

## Context
Use [Stage 7](../../implementation-plan.md#9-stage-7-wpf-operator-interface) and [Security Tests](../../test-plan.md#14-security-and-authorization-tests).

## Scope
- Display effective/direct platform, account, Portfolio, and Bot switch state and history.
- Implement activation/deactivation requiring authority, reason, exact-scope confirmation, and fresh version.
- Distinguish inherited blocks, pending, success, denial, and concurrency outcomes.
- Add accessibility metadata and view-model/application integration tests.

## Out of Scope
None.

## Acceptance Criteria
- Confirmation identifies exact scope and action.
- Completion refreshes effective hierarchy and immutable history.
- Unauthorized controls disclose no inaccessible scope details.

## Validation
Build; KillSwitchUi WPF tests; OperatorKillSwitch integration tests; full tests; format.

## Completion Notes
Implemented an accessible hierarchical kill-switch workspace for platform, Broker Account, Portfolio, and Trading Bot
scopes. The view distinguishes direct and inherited effective state, pending and terminal outcomes, and immutable history.
Activation and clearing require authority, a non-empty reason, the fresh direct-state version, and a case-sensitive exact
action-and-scope confirmation. Successful changes refresh the authorized scope list, effective hierarchy, and history;
denied refreshes clear all previously visible scope facts. Extended the operator command boundary to carry the audited
confirmation separately from the reason.

Validation completed on 2026-08-22:

- `.\dev.ps1 restore` — passed in locked mode after Docker Desktop recovery.
- `.\dev.ps1 build` — passed with zero warnings and zero errors, including the combined S7-001–S7-012 integration.
- `.\dev.ps1 test -Project tests/Trading.UI.Wpf.Tests -Filter TestCategory=KillSwitchUi` — 3 passed.
- `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter TestCategory=OperatorKillSwitch` — 2 passed.
- `.\dev.ps1 test` — 1,215 passed, with four existing pending Stage 7 acceptance scenarios skipped.
- `.\dev.ps1 format` — passed.
- `git diff --check` — passed.

Docker Desktop initially had no running engine; the previously authorized Docker Desktop/WSL recovery restored its Linux
engine before validation. No test was retried. Windows-only interactive UI validation remains assigned to S7-017.

Deviations: none. Follow-up tasks: none. ADRs: none.
