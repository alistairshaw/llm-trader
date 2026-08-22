---
schema_version: 1
id: S6-012
title: Recover durable paper execution after restart
stage: 6
status: done
priority: 760
type: feature
depends_on: [S6-011]
labels: [recovery, restart, leases, reconciliation]
created: 2026-08-21
updated: 2026-08-22
owner: agent/s6-012
---

# S6-012: Recover Durable Paper Execution after Restart

## Objective

Resume every incomplete paper execution boundary safely after process termination or graceful shutdown.

## Context

Use [Implementation Plan — Stage 6](../../implementation-plan.md#8-stage-6-paper-order-execution), [Data Model — Unit of Work and Transactions](../../data-model.md#13-unit-of-work-and-transactions), and [Local Development — Application Execution](../../local-development.md#2-application-execution).

## Scope

- Recover expired inbox/outbox claims, incomplete submissions, unknown outcomes, pending reconciliation, unapplied executions, and unfinished Reservation release work.
- Reconstruct the next action solely from durable state and append recovery audit records with stable reasons.
- Start workers only after migrations and required broker-account reconciliation complete.
- Stop new claims on cancellation, finish bounded in-flight persistence, release or expire leases, and leave remaining work recoverable.
- Add deterministic termination points around every material transaction and broker-I/O boundary.

## Acceptance Criteria

- Restart tests at each Stage 6 boundary converge to one correct Order and financial result.
- Accepted-before-timeout recovery reconciles by client ID and never duplicates submission.
- Pending inbox and outbox work resumes without duplicate state changes.
- Graceful shutdown leaves no ambiguous owned lease or uncommitted financial state.
- Recovery remains isolated by broker account, Portfolio, and Order.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ExecutionRecovery"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=PaperExecutionRestart"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented deterministic paper-execution startup recovery. Expired claimed submissions are atomically transitioned
through `Submitting` to `Unknown`, the original submission work is terminalized with a stable recovery reason, and one
client-ID-keyed reconciliation item is created or restored before submission can run again. Other expired inbox and
outbox leases return to pending with cleared ownership; failed poison items remain isolated and are reported.

Added an account-scoped recovery coordinator that runs durable recovery, required account reconciliation, immutable
bounded recovery audit, outbox drain, and deferred inbox/fill drain in readiness order. Explicit checkpoints cover the
durable transaction, account broker I/O, and worker boundaries. Cancellation stops new claims, while the existing
processors return owned work to recoverable retry state.

Updated architecture, data-model, and local-development documentation. No migration or ADR was required.

Validation on Linux through Docker:

- `./dev.ps1 build` — passed, zero warnings and errors.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ExecutionRecovery"` — 3 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ExecutionRecovery"` — 2 passed.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=PaperExecutionRestart"` — 1 passed.
- `./dev.ps1 test` — 1,104 passed, 34 Stage 6 scenarios pending later acceptance binding, zero failures.
- `./dev.ps1 format` — passed.
- `git diff --check` — passed.

Windows CI remains delegated to the Stage 6 review task. No deviations or follow-up tasks.
