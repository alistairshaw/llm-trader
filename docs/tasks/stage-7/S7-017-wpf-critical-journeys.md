---
schema_version: 1
id: S7-017
title: Automate critical WPF operator journeys
stage: 7
status: blocked
priority: 740
type: test
depends_on: [S7-015, S7-016, S7-019]
labels: [wpf, flaui, acceptance]
created: 2026-08-22
updated: 2026-08-22
owner: Codex/s7_017
blocked_reason: Published WPF composition has no authorized operator workflow or authorization registrations and does not wire Portfolio or Execution workspaces.
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
Implementation did not begin because inspection of the published application composition established that
`HostBootstrap` does not register `IOperatorAuthorization`, `IOperatorWorkflowPort`, `AuthorizedOperatorService`,
or `OperatorPrincipal`. `App.xaml.cs` therefore cannot resolve its required operator query and command services and
falls back to placeholder navigation. The same composition also omits Portfolio and Execution workspace factories.

`S7-019` records the required production composition and deterministic-profile repair. S7-017 remains blocked until
that task is done; no WPF scenario was activated, skipped, or represented as passing.

Validation performed: repository inspection with `rg` and `Get-Content`; no build or test command was applicable to
this documentation-only blocker record. Deviations: journey bindings were not implemented. Follow-up: `S7-019`.
