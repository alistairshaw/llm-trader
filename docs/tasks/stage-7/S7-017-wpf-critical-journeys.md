---
schema_version: 1
id: S7-017
title: Automate critical WPF operator journeys
stage: 7
status: planned
priority: 740
type: test
depends_on: [S7-015, S7-016]
labels: [wpf, flaui, acceptance]
created: 2026-08-22
updated: 2026-08-22
---
# S7-017: Automate Critical WPF Operator Journeys

## Objective
Activate every required WPF scenario against the published deterministic application.

## Context
Use [Stage 7](../../implementation-plan.md#9-stage-7-wpf-operator-interface), [WPF UI Acceptance](../../test-plan.md#11-wpf-ui-acceptance-tests), and Stage 7 traceability.

## Scope
- Bind Bot/configuration/pause/resume, assignment, run, Research, Proposal decisions, execution, warnings, modes, and switch journeys.
- Verify Orders/Fills update without restart and shutdown preserves recoverable state.
- Verify Automation IDs, names, roles, state, keyboard navigation, bounded waits, and dialogs.
- Remove WPF pending tags and synchronize generated sources.

## Out of Scope
None.

## Acceptance Criteria
- Every WPF scenario passes twice on interactive Windows with zero skips.
- Approval shows the reviewed Proposal approved and resulting paper Order visible.
- Failures retain complete bounded redacted artifacts.

## Validation
Build; publish-wpf; Stage7 WPF acceptance twice; full tests; format.

## Completion Notes
Pending implementation.

