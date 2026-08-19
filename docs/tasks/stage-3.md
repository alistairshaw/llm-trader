# Stage 3 Backlog: Multi-Bot Runtime and Scheduling

Stage goal and exit criteria: [Implementation Plan — Stage 3](../implementation-plan.md#5-stage-3-multi-bot-runtime-and-scheduling).

Task workflow and priority rules: [Task Management](../task-management.md).

## Current Next Task

`S3-002` — Define runtime, model, and tool contracts.

## Ordered Backlog

| ID | Task | Status | Priority | Depends on |
| --- | --- | --- | ---: | --- |
| [`S3-001`](stage-3/S3-001-write-stage-3-gherkin.md) | Write Stage 3 executable Gherkin specifications | Done | 1000 | — |
| [`S3-002`](stage-3/S3-002-runtime-contracts.md) | Define runtime, model, and tool contracts | Ready | 930 | `S3-001` |
| [`S3-003`](stage-3/S3-003-runtime-migration.md) | Add Bot Run persistence migration | Planned | 910 | `S3-002` |
| [`S3-004`](stage-3/S3-004-run-trigger-repositories.md) | Implement durable Bot Run, trigger, and lease repositories | Planned | 890 | `S3-003` |
| [`S3-005`](stage-3/S3-005-scheduling-policy.md) | Implement deterministic scheduling policy | Planned | 860 | `S3-002` |
| [`S3-006`](stage-3/S3-006-trigger-coalescing.md) | Implement durable trigger ingestion and coalescing | Planned | 840 | `S3-004`, `S3-005` |
| [`S3-007`](stage-3/S3-007-run-input.md) | Build deterministic Bot Run input | Planned | 820 | `S3-004` |
| [`S3-008`](stage-3/S3-008-tool-dispatch.md) | Implement authorized Stage 3 tool dispatch | Planned | 800 | `S3-002`, `S3-007` |
| [`S3-009`](stage-3/S3-009-scripted-model-loop.md) | Implement the scripted bounded model loop | Planned | 780 | `S3-008` |
| [`S3-010`](stage-3/S3-010-run-orchestration.md) | Orchestrate one complete Trading Bot run | Planned | 760 | `S3-006`, `S3-009` |
| [`S3-011`](stage-3/S3-011-multi-bot-supervisor.md) | Implement isolated multi-bot supervision | Planned | 740 | `S3-010` |
| [`S3-012`](stage-3/S3-012-recovery-shutdown.md) | Implement lease recovery and graceful shutdown | Planned | 720 | `S3-011` |
| [`S3-013`](stage-3/S3-013-headless-host.md) | Run configured bots through the headless host | Planned | 700 | `S3-012` |
| [`S3-014`](stage-3/S3-014-runtime-acceptance.md) | Complete Stage 3 runtime acceptance bindings | Planned | 680 | `S3-013` |
| [`S3-015`](stage-3/S3-015-stage-3-review.md) | Complete Stage 3 acceptance and review | Planned | 1000 | `S3-001`–`S3-014` |

## Stage Exit Gate

- Every Stage 3 task is `done`.
- Every Stage 3 Reqnroll scenario passes on Windows and Linux with no pending or skipped scenario.
- The Stage 3 migration succeeds against a new database and the completed Stage 2 fixture.
- Scripted model tests prove deterministic tool authorization, budget enforcement, safe termination, scheduling, isolation, recovery, and shutdown.
- The headless host starts configured bots and shuts down cleanly in simulated mode.
- Release build, formatting, architecture, unit, data, integration, acceptance, migration, and security gates pass.
- Hosted Windows and Linux CI passes on the completed Stage 3 revision.
- The Stage 3 Review Record approves beginning Stage 4.

## Completion Summary

Not started.
