---
schema_version: 1
id: S3-012
title: Implement lease recovery and graceful shutdown
stage: 3
status: ready
priority: 720
type: feature
depends_on: [S3-011]
labels: [recovery, leases, shutdown]
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

Not completed.
