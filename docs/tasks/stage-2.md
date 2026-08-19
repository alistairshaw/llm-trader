# Stage 2 Backlog: Persistence and Portfolio State

Stage goal and exit criteria: [Implementation Plan — Stage 2](../implementation-plan.md#4-stage-2-persistence-and-portfolio-state).

Task workflow and priority rules: [Task Management](../task-management.md).

## Current Next Task

`S2-012` — Implement the restart-safe portfolio persistence workflow.

## Ordered Backlog

| ID | Task | Status | Priority | Depends on |
| --- | --- | --- | ---: | --- |
| [`S2-001`](stage-2/S2-001-write-stage-2-gherkin.md) | Write Stage 2 executable Gherkin specifications | Done | 1000 | — |
| [`S2-002`](stage-2/S2-002-persistence-contracts.md) | Define persistence contracts and results | Done | 920 | `S2-001` |
| [`S2-003`](stage-2/S2-003-ef-sqlite-infrastructure.md) | Configure EF Core and SQLite infrastructure | Done | 900 | `S2-002` |
| [`S2-004`](stage-2/S2-004-persistence-converters.md) | Implement canonical persistence converters | Done | 880 | `S2-003` |
| [`S2-005`](stage-2/S2-005-initial-migration.md) | Create and verify the initial persistence migration | Done | 860 | `S2-004` |
| [`S2-006`](stage-2/S2-006-broker-instrument-persistence.md) | Persist Broker and Instrument aggregates | Done | 820 | `S2-005` |
| [`S2-007`](stage-2/S2-007-trading-bot-persistence.md) | Persist Trading Bots and configuration versions | Done | 810 | `S2-005` |
| [`S2-008`](stage-2/S2-008-portfolio-ledger-persistence.md) | Persist Portfolios, Positions, and ledger entries | Done | 800 | `S2-006`, `S2-007` |
| [`S2-009`](stage-2/S2-009-decision-snapshot-persistence.md) | Persist immutable Portfolio Decision Snapshots | Done | 790 | `S2-008` |
| [`S2-010`](stage-2/S2-010-concurrency-transactions.md) | Implement concurrency and transaction boundaries | Done | 760 | `S2-008`, `S2-009` |
| [`S2-011`](stage-2/S2-011-portfolio-read-models.md) | Implement no-tracking portfolio read models | Done | 740 | `S2-009` |
| [`S2-012`](stage-2/S2-012-persistence-workflow.md) | Implement the restart-safe portfolio persistence workflow | Ready | 720 | `S2-010`, `S2-011` |
| [`S2-013`](stage-2/S2-013-stage-2-acceptance-review.md) | Complete Stage 2 acceptance and review | Planned | 1000 | `S2-001`–`S2-012` |

## Stage Exit Gate

- Every Stage 2 task is `done`.
- Every Stage 2 Reqnroll scenario passes on Windows and Linux with no pending or skipped scenario.
- The initial migration succeeds against a new database and the empty Stage 1 upgrade fixture.
- Persistence integration tests use isolated SQLite databases and the real EF Core SQLite provider.
- Release build, formatting, architecture tests, unit tests, integration tests, and acceptance tests pass.
- Hosted Windows and Linux CI passes on the completed Stage 2 revision.
- The Stage 2 Review Record approves beginning Stage 3.

## Completion Summary

Not started.
