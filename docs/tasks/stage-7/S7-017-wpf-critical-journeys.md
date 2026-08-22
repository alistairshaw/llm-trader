---
schema_version: 1
id: S7-017
title: Automate critical WPF operator journeys
stage: 7
status: blocked
priority: 740
type: test
depends_on: [S7-015, S7-016, S7-019, S7-020, S7-021]
labels: [wpf, flaui, acceptance]
created: 2026-08-22
updated: 2026-08-22
owner: Codex/s7_017
blocked_reason: Windows CI requires exact SQLite lifecycle release and deterministic paper-journey readiness repairs recorded by S7-020 and S7-021.
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
Activated all 19 expanded Stage 7 WPF scenarios by removing the pending tags and synchronizing their generated
Reqnroll sources. Added thin journey bindings over a shared FlaUI driver and Automation-ID page object operations for
Bot configuration/lifecycle, Portfolio assignment, terminal runs, exact Research, Proposal evidence and decisions,
paper Orders/Fills, execution modes, operational warnings, Portfolio kill switches, accessibility/keyboard state, and
recoverable shutdown/restart. The driver uses bounded readiness, interaction, and shutdown waits; retains bounded
redacted screenshots, UIA trees, logs, and scenario context on failure; and deletes owned processes and fixture data on
the first teardown attempt. Extended the deterministic production workflow projection with exact run, Research,
Proposal, decision, reservation, and Portfolio kill-switch details. Live mode remains read-only and explicitly cannot
be selected from the local operator UI. CI now runs the complete Stage 7 WPF category twice on interactive Windows.

Validation:

- `./dev.ps1 build` — passed, zero warnings and errors; generated Reqnroll sources synchronized.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests/Trading.IntegrationTests.csproj -Filter
  "FullyQualifiedName~OperatorProductionCompositionTests"` — 2 passed.
- `./dev.ps1 test -Project tests/Trading.UI.Wpf.Tests/Trading.UI.Wpf.Tests.csproj` — 40 passed.
- `./dev.ps1 test` — 1,229 passed, zero failures and skips.
- `./dev.ps1 format` — passed with no violations.
- `./dev.ps1 publish-wpf` — passed; final self-contained `win-x64` artifact produced.

The Windows host has no declared .NET UI-test runner, so the interactive Stage 7 WPF category could not be executed
locally. It remains delegated to the new exact Windows CI step, which runs it twice and retains TRX and bounded failure
artifacts. No scenarios were weakened and no network or live-order authority was added.

Hosted evidence: candidate `d2f27d78ba0516531305e001221d6bbd2131f4f5` failed Windows CI run `32605316561`,
job `97109412699`. Linux and secret scanning passed. `S7-020` records the exact SQLite ownership failure and `S7-021`
records the deterministic readiness/fixture failures. The S7-017 harness repair now waits by owned process identity
instead of a disposed `Process` object and makes screenshot/UIA-tree artifact capture non-throwing for stale elements.
S7-017 remains blocked until both repair tasks are done and a new hosted candidate passes both Windows WPF executions.
