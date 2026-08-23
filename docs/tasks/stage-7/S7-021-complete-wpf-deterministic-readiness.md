---
schema_version: 1
id: S7-021
title: Complete deterministic WPF paper journey before readiness
stage: 7
status: done
priority: 985
type: defect
depends_on: [S7-019]
labels: [wpf, fixtures, readiness, paper-trading]
created: 2026-08-22
updated: 2026-08-22
---
# S7-021: Complete Deterministic WPF Paper Journey Before Readiness

## Objective
Publish WPF readiness only after the deterministic profile has completed its exact Research-to-Proposal-to-paper-Fill fixture journey.

## Context
Windows CI run `32605316561`, job `97109412699`, showed disabled Proposal review actions and an empty Execution
selection. `TradingRuntimeHostedService` currently marks WPF readiness before it creates the deterministic Proposal,
Order, and Fills, and only the headless smoke path executes that fixture workflow. Follow
[Architecture](../../architecture.md), [Local Development](../../local-development.md), and
[Test Plan](../../test-plan.md).

## Scope
- Execute the deterministic WPF profile's Bot runs, Research publication, awaiting Proposal, approved paper Order,
  partial Fill, final Fill, projections, and recoverable active-work fixture before `RuntimeReadiness.MarkReady`.
- Keep the WPF process running after readiness; do not use the headless smoke path's application-stop behavior.
- Use the injected fixture clock for Proposal eligibility and every time-dependent WPF view model.
- Ensure the Proposal queue has a selected eligible exact version with enabled review/decision actions after UI selection.
- Ensure the Execution workspace contains the completed paper Order and exactly two deduplicated Fills at readiness.
- Preserve Research-only, human-approval, paper, and read-only live-mode representation plus stable warning fixture states.
- Add production-composed readiness tests for exact Proposal, Order, Fill, mode, warning, and restart facts.
- Keep the fixture network-free with the simulated paper broker and zero live submissions.

## Out of Scope
- FlaUI selectors, page objects, artifact capture, and process cleanup.
- SQLite pool ownership after host disposal.

## Acceptance Criteria
- The WPF ready signal is written only after the exact awaiting Proposal and completed paper Order with two Fills are queryable.
- Proposal review and decision commands are enabled after selecting and opening the fixture Proposal.
- Execution selection and detail loading expose the Filled Order and exactly two Fills without restarting.
- The profile remains running until operator close and restarts into consistent recoverable state.
- No public network, credential, live broker, or live-order authority is present.
- Build, focused profile/readiness tests, full tests, publish, and format pass.

## Validation
- `./dev.ps1 build`
- Focused WPF profile, runtime-readiness, Proposal, and paper execution integration tests
- `./dev.ps1 publish-wpf`
- `./dev.ps1 test`
- `./dev.ps1 format`
- Interactive Windows WPF Proposal and execution journeys

## Completion Notes
The WPF deterministic profile now completes both Bot runs, fixture Research publication, Proposal governance, the
approved paper Order, partial and final deduplicated Fills, projections, and a separate evaluated Proposal awaiting
operator review before publishing runtime readiness. WPF remains alive after readiness, while headless smoke retains
its stop-on-completion behavior and original fixture cardinalities. Restart verifies the exact durable awaiting
Proposal and Filled Order facts and does not replay deterministic identifiers.

The WPF application now supplies the host fixture clock to Proposal eligibility and Portfolio staleness. Parameterless
nullable async commands accept WPF's null command parameter, and Proposal decision commands enable only for a
confirmed, exact, unexpired `AwaitingHumanApproval` detail. The profile remains fixture-only with the simulated paper
broker, no credential or network configuration, and no live-order authority.

Validation:

- `./dev.ps1 restore` — passed in locked mode.
- `./dev.ps1 build` — passed with zero warnings and errors.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests/Trading.IntegrationTests.csproj -Filter "Category=WpfTestProfile|Category=WpfHostLifecycle"` — 4 passed.
- `./dev.ps1 test -Project tests/Trading.UI.Wpf.Tests/Trading.UI.Wpf.Tests.csproj -Filter "Category=ProposalReview"` — 4 passed.
- `./dev.ps1 test` — 1,229 passed with zero failures and skips.
- `./dev.ps1 publish-wpf` — passed; self-contained `win-x64` artifact produced.
- `./dev.ps1 format` — passed with no violations.

The interactive Windows FlaUI journeys remain owned by `S7-017`, as specified by this task's out-of-scope boundary.
No deviations, follow-up tasks, or ADRs.
