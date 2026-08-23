# Stage 7 Backlog: WPF Operator Interface

Stage goal and exit criteria: [Implementation Plan — Stage 7](../implementation-plan.md#9-stage-7-wpf-operator-interface).

Task workflow: [Task Management](../task-management.md).

## Current Next Task

[`S7-018`](stage-7/S7-018-stage-7-review.md) is in review pending exact-revision hosted validation.

## Ordered Backlog

| ID | Task | Status | Priority | Depends on |
| --- | --- | --- | ---: | --- |
| [`S7-001`](stage-7/S7-001-write-stage-7-gherkin.md) | Write executable Gherkin specifications | Done | 1000 | — |
| [`S7-002`](stage-7/S7-002-operator-application-contracts.md) | Define authorized operator application contracts | Done | 960 | `S7-001` |
| [`S7-003`](stage-7/S7-003-operational-kill-switches.md) | Implement durable hierarchical kill switches | Done | 940 | `S7-002` |
| [`S7-004`](stage-7/S7-004-wpf-host-lifecycle.md) | Compose WPF with the Generic Host | Done | 920 | `S7-002` |
| [`S7-005`](stage-7/S7-005-shell-navigation-accessibility.md) | Build shell, navigation, and accessibility foundations | Done | 900 | `S7-004` |
| [`S7-006`](stage-7/S7-006-bot-management.md) | Build Trading Bot management | Done | 860 | `S7-002`, `S7-005` |
| [`S7-007`](stage-7/S7-007-portfolio-broker-status.md) | Build Portfolio and broker status | Done | 860 | `S7-002`, `S7-005` |
| [`S7-008`](stage-7/S7-008-bot-run-operations.md) | Build Bot Run operations and status | Done | 850 | `S7-002`, `S7-005` |
| [`S7-009`](stage-7/S7-009-research-catalog.md) | Build Research catalog and Report viewer | Done | 850 | `S7-002`, `S7-005` |
| [`S7-011`](stage-7/S7-011-execution-risk-audit.md) | Build execution, Fill, and risk audit views | Done | 840 | `S7-002`, `S7-005` |
| [`S7-010`](stage-7/S7-010-proposal-review.md) | Build Proposal review and human decisions | Done | 840 | `S7-002`, `S7-005` |
| [`S7-012`](stage-7/S7-012-kill-switch-ui.md) | Build authorized kill-switch controls | Done | 830 | `S7-003`, `S7-005`, `S7-007` |
| [`S7-013`](stage-7/S7-013-live-ui-updates.md) | Deliver live operator updates through the UI dispatcher | Done | 820 | `S7-006`–`S7-012` |
| [`S7-014`](stage-7/S7-014-wpf-test-profile.md) | Publish deterministic WPF test profile | Done | 800 | `S7-013` |
| [`S7-015`](stage-7/S7-015-flaui-harness.md) | Build Windows FlaUI automation harness | Done | 780 | `S7-014` |
| [`S7-016`](stage-7/S7-016-production-acceptance-bindings.md) | Complete production-backed non-UI acceptance | Done | 760 | `S7-003`, `S7-013` |
| [`S7-019`](stage-7/S7-019-compose-operator-wpf-workspaces.md) | Compose authorized operator workflows and every WPF workspace | Done | 970 | `S7-015`, `S7-016` |
| [`S7-020`](stage-7/S7-020-release-wpf-sqlite-ownership.md) | Release WPF SQLite ownership on lifecycle stop | Done | 990 | `S7-019` |
| [`S7-021`](stage-7/S7-021-complete-wpf-deterministic-readiness.md) | Complete deterministic WPF paper journey before readiness | Done | 985 | `S7-019` |
| [`S7-022`](stage-7/S7-022-authorize-wpf-research-fixture.md) | Authorize deterministic WPF Research fixture identities | Done | 988 | `S7-019`, `S7-021` |
| [`S7-017`](stage-7/S7-017-wpf-critical-journeys.md) | Automate critical WPF operator journeys | Done | 740 | `S7-015`, `S7-016`, `S7-019`, `S7-020`, `S7-021`, `S7-022` |
| [`S7-018`](stage-7/S7-018-stage-7-review.md) | Complete Stage 7 acceptance and review | Review | 1000 | All implementation tasks |

## Stage Exit Gate

- All tasks and Stage 7 scenarios are complete with zero unapproved skips.
- Non-UI scenarios pass on Windows and Linux; WPF scenarios pass in interactive Windows CI.
- View models pass tests without launching WPF; critical controls expose stable accessibility metadata.
- Operator actions remain authorized, audited, asynchronous, cancellable, and Engine-mediated.
- Live UI updates, bounded host shutdown, WPF publish, migrations, full tests, and security gates pass.
- The Stage 7 Review Record approves Stage 8.
