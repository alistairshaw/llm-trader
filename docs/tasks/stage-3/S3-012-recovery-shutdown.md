---
schema_version: 1
id: S3-012
title: Implement lease recovery and graceful shutdown
stage: 3
status: done
priority: 720
type: feature
depends_on: [S3-011]
labels: [recovery, leases, shutdown]
owner: codex-s3-012
created: 2026-08-19
updated: 2026-08-19
---

# S3-012: Implement Lease Recovery and Graceful Shutdown

## Objective

Recover abandoned Bot Runs safely and stop active runtime work without losing durable state.

## Context

Follow [Architecture — Runtime Model](../../architecture.md#8-runtime-model), [Concurrency and Messaging](../../architecture.md#14-concurrency-and-messaging), and Stage 3 recovery criteria.

## Scope

- Implement startup discovery of active runs whose lease expiry is at or before the injected current UTC time.
- Reconcile each expired run into a deterministic terminal or resumable outcome based on its persisted checkpoint; Stage 3 resumes only before model execution and faults runs interrupted after model execution began.
- Preserve existing transcript, tool history, usage, triggers, and pinned identities during recovery.
- Release recoverable Bot eligibility and retain pending triggers for one new run.
- Implement shutdown that rejects new claims, stops scheduler production, completes the supervisor writer, cancels active loops, waits within a configured timeout, persists terminal cancellation or checkpoint state, and disposes services.
- Add restart and shutdown integration tests against file-backed SQLite with deterministic gates and clocks.

## Acceptance Criteria

- A live unexpired lease is never stolen.
- An expired pre-model run becomes eligible exactly once without duplicate audit facts.
- An expired run interrupted after model execution began becomes a safe terminal fault and is never replayed implicitly.
- Shutdown acknowledges no new work after stop begins.
- Active loops receive cancellation and durable triggers remain unconsumed or are retained for recovery.
- Restart reconstructs the same terminal/checkpoint state without duplicate runs.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=RuntimeRecoveryOrShutdown"
.\dev.ps1 build
```

## Completion Notes

- Added deterministic startup recovery that discovers expired leases, atomically faults the abandoned run and retains one deduplicated follow-up trigger only for pre-model checkpoints. Runs interrupted during reasoning or tool execution are terminalized without implicit replay, while their pinned identities, transcript, usage, trigger, and tool audit remain intact.
- Added a transactional SQLite recovery repository operation so lease release and follow-up work retention cannot be separated by a process failure. Optimistic version checks prevent recovery from stealing a renewed live lease.
- Added bounded supervisor shutdown that rejects admission immediately, completes its writer, drains within the configured deadline, propagates cancellation after timeout, completes queued work safely, and reports whether the drain completed.
- Added deterministic integration coverage for pre-model restart recovery, post-model safe faulting, repeat recovery idempotency, and follow-up retention, plus supervisor drain/deadline/cancellation coverage.
- Validation: `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=RuntimeRecoveryOrShutdown"` (2 passed); `.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=MultiBotSupervisor"` (7 passed); `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage3Migrations"` (5 passed); `.\dev.ps1 build` (succeeded, 0 warnings/errors); `.\dev.ps1 test` (622 passed, 26 Stage 3 acceptance scenarios pending, 0 failed); `.\dev.ps1 format` (passed); `git diff --check` (passed).
- Deviations: none. Follow-up tasks: none. ADRs: none.
