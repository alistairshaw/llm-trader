# Stage 4 Backlog: Shared Research Bot

Stage goal and exit criteria: [Implementation Plan — Stage 4](../implementation-plan.md#6-stage-4-shared-research-bot).

Task workflow and priority rules: [Task Management](../task-management.md).

## Current Next Task

`S4-007` — Implement authorized Research tool dispatch.

## Ordered Backlog

| ID | Task | Status | Priority | Depends on |
| --- | --- | --- | ---: | --- |
| [`S4-001`](stage-4/S4-001-write-stage-4-gherkin.md) | Write Stage 4 executable Gherkin specifications | Done | 1000 | — |
| [`S4-002`](stage-4/S4-002-research-contracts.md) | Define Research runtime and publication contracts | Done | 930 | `S4-001` |
| [`S4-003`](stage-4/S4-003-research-persistence.md) | Add the Stage 4 Research persistence migration | Done | 910 | `S4-002` |
| [`S4-004`](stage-4/S4-004-research-repositories.md) | Implement Research repositories and authorized catalog | Done | 890 | `S4-003` |
| [`S4-005`](stage-4/S4-005-request-service.md) | Implement authorized request deduplication and reuse | Done | 870 | `S4-004` |
| [`S4-006`](stage-4/S4-006-fixture-sources.md) | Implement fixture-backed approved research sources | Done | 850 | `S4-002` |
| [`S4-007`](stage-4/S4-007-research-tool-dispatch.md) | Implement authorized Research tool dispatch | Ready | 830 | `S4-005`, `S4-006` |
| [`S4-008`](stage-4/S4-008-scripted-research-loop.md) | Implement the scripted bounded Research loop | Planned | 810 | `S4-007` |
| [`S4-009`](stage-4/S4-009-report-publication.md) | Validate and publish immutable Research reports | Planned | 790 | `S4-008` |
| [`S4-010`](stage-4/S4-010-subscriber-notifications.md) | Deliver durable subscriber notifications and Bot triggers | Planned | 770 | `S4-009` |
| [`S4-011`](stage-4/S4-011-research-orchestration.md) | Orchestrate Research runs and restart recovery | Planned | 750 | `S4-010` |
| [`S4-012`](stage-4/S4-012-trading-bot-research-tools.md) | Add Trading Bot Research tools and report consumption | Planned | 730 | `S4-005`, `S4-010` |
| [`S4-013`](stage-4/S4-013-headless-research-host.md) | Run shared Research through the headless host | Planned | 710 | `S4-011`, `S4-012` |
| [`S4-014`](stage-4/S4-014-research-acceptance.md) | Complete Stage 4 Research acceptance bindings | Planned | 690 | `S4-013` |
| [`S4-015`](stage-4/S4-015-stage-4-review.md) | Complete Stage 4 acceptance and review | Planned | 1000 | `S4-001`–`S4-014` |

## Stage Exit Gate

- Every Stage 4 task is `done`.
- Every Stage 4 Reqnroll scenario passes on Windows and Linux with no pending or skipped scenario.
- The Stage 4 migration succeeds against a new database and the completed Stage 3 fixture.
- Deterministic tests prove request authorization, safe deduplication, fresh reuse, visibility isolation, immutable publication, provenance, prompt-injection resistance, bounded execution, durable notification, recovery, and Trading Bot consumption.
- The headless host demonstrates two Trading Bots sharing one fixture-backed report, private visibility, refresh/versioning, and graceful shutdown.
- Release build, formatting, architecture, unit, data, Research, Engine, integration, acceptance, migration, and security gates pass.
- Hosted Windows and Linux CI passes on the completed Stage 4 revision.
- The Stage 4 Review Record approves beginning Stage 5.
