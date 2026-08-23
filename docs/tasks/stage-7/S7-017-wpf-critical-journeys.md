---
schema_version: 1
id: S7-017
title: Automate critical WPF operator journeys
stage: 7
status: review
priority: 740
type: test
depends_on: [S7-015, S7-016, S7-019, S7-020, S7-021]
labels: [wpf, flaui, acceptance]
created: 2026-08-22
updated: 2026-08-22
owner: Codex/s7_017
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
That candidate remained blocked until the two repair implementations were integrated.

Candidate revalidation after integrating `S7-020` and `S7-021`: locked restore passed; Release build passed with zero
warnings and errors; `TestCategory=WpfHostLifecycle` passed 5/5; `TestCategory=ProposalReview` passed 4/4; the full
suite passed 1,230/1,230 with zero skips; format passed; and the final self-contained WPF publish passed. S7-017 is in
review pending the exact hosted Windows lifecycle result and both interactive Stage 7 WPF executions. No local UI pass
is claimed.

Hosted candidate `ebeb2934ff63550640300c4942158660095d11da` passed Linux and the Windows complete
cross-platform suite, native build, self-contained publish, and harness smoke in CI run `32607116964`. Its first
interactive Stage 7 pass exposed five deterministic harness defects: three busy indicators published their accessible
names instead of their bound state, the operational-warning Given expression also matched the Portfolio-assignment
precondition, and the virtualized Fills grid was queried before its tab was selected. Busy indicators now publish the
bound state through UIA ItemStatus, the page object reads that property, the warning binding accepts only its four
specified conditions, and stable execution tab Automation IDs allow the journey to select Fills before bounded
polling. Static accessibility tests cover the new UIA contract.

Revalidation after those repairs: Release build passed with zero warnings and errors; WPF unit tests passed 40/40; the
full suite passed 1,230/1,230 with zero skips; format passed; and the final self-contained WPF publish passed. S7-017
remains in review pending a new hosted run in which both interactive Stage 7 passes complete. No local UI pass is
claimed.

Hosted candidate `752e900b9308d507c06774afd29db7934c234cb7` passed Linux and the Windows complete
cross-platform suite, native build, self-contained publish, and harness smoke in CI run `32608003940`. Its first
interactive Stage 7 pass reduced the remaining failures to two detail-selection races: the run journey read the
accessible label instead of authoritative status state, and both run and Research journeys could invoke their detail
commands before WPF propagated the selected row. The inspect controls now expose the selected immutable identity via
UIA ItemStatus; the harness waits for that identity before invoking, and the authoritative status and exact Report
identity are separately exposed and boundedly polled through ItemStatus. Static accessibility tests enforce these
selection and detail-state contracts.

Revalidation after the selection synchronization repair: Release build passed with zero warnings and errors; WPF unit
tests passed 40/40; the full suite passed 1,230/1,230 with zero skips; format passed; and the final self-contained WPF
publish passed. S7-017 remains in review pending a hosted run in which both interactive Stage 7 passes complete. No
local UI pass is claimed.
