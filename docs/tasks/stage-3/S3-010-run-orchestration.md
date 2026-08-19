---
schema_version: 1
id: S3-010
title: Orchestrate one complete Trading Bot run
stage: 3
status: done
priority: 760
type: feature
depends_on: [S3-006, S3-009]
labels: [orchestration, bot-run, audit]
created: 2026-08-19
updated: 2026-08-19
---

# S3-010: Orchestrate One Complete Trading Bot Run

## Objective

Coordinate trigger claim, lease, pinned input, bounded reasoning, scheduling, persistence, and follow-up eligibility for one Bot.

## Context

Implement [Trading Bot — Run Workflow](../../trading-bot.md#7-run-workflow) through Engine application services and Stage 2 persistence boundaries.

## Scope

- Implement a Bot Run application service that validates Bot eligibility, claims triggers and lease, pins active configuration and latest authorized snapshot, and persists the run before reasoning.
- Build deterministic input and execute the scripted bounded model loop outside database transactions.
- Renew the lease at deterministic checkpoints and cancel execution when ownership is lost.
- Persist tool audit facts, transcript, usage, finish or safe terminal outcome, and release the lease atomically.
- Evaluate the requested wake through scheduling policy and persist requested and accepted decisions separately.
- Retain triggers arriving during the run for one follow-up claim.
- Add integration tests for completed, no-action, timed-out, budget-exceeded, cancelled, faulted, schedule-adjusted, and lost-lease runs.

## Acceptance Criteria

- Every run pins exactly one immutable configuration and snapshot before model execution.
- No database transaction remains open during a model call or tool execution.
- Every terminal path releases or expires the lease and preserves a reconstructable audit record.
- A timed-out, failed, or incomplete run creates no implicit proposal, reservation, or order.
- Requested and scheduler-accepted times remain distinct persisted facts.
- Triggers received during execution remain durable for one follow-up run.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=BotRunOrchestration"
.\dev.ps1 build
```

## Completion Notes

Implemented `BotRunOrchestrationService` to select the active configuration and latest matching decision snapshot, atomically claim eligible triggers and a per-Bot lease, persist deterministic pinned input, renew and verify lease ownership, execute the bounded model loop outside persistence calls, evaluate requested wake times after terminal persistence, and durably retain requested/accepted schedule facts. Safe pre-reasoning and active-run failure handling releases owned leases, while ownership loss stops before model or tool execution. EF runtime repositories now clear tracked state after direct input-audit and lease updates so subsequent optimistic writes use authoritative versions.

Validation completed on 2026-08-19:

- `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=BotRunOrchestration"` — passed 9, failed 0, skipped 0.
- `.\dev.ps1 test -Project tests/Trading.Engine.Tests` — passed 45, failed 0, skipped 0.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests` — passed 105, failed 0, skipped 0.
- `.\dev.ps1 test -Project tests/Trading.IntegrationTests` — passed 14, failed 0, skipped 0.
- `.\dev.ps1 build` — Release build succeeded with 0 warnings and 0 errors.
- `.\dev.ps1 test` — passed 613, failed 0, with 26 pending Stage 3 acceptance scenarios.
- `.\dev.ps1 format` — passed with no formatter or analyzer findings (required host Docker-config access).

No persistence schema changed, so migration drift validation was not applicable. No scope deviations, follow-up tasks, or ADR changes.
