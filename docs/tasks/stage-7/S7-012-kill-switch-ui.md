---
schema_version: 1
id: S7-012
title: Build authorized kill-switch controls
stage: 7
status: planned
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
Pending implementation.

