---
schema_version: 1
id: S6-008
title: Submit paper orders with stable client identities
stage: 6
status: done
priority: 840
type: feature
depends_on: [S6-006, S6-007]
labels: [submission, idempotency, client-order-id, paper]
created: 2026-08-21
updated: 2026-08-22
owner: s6_008
---

# S6-008: Submit Paper Orders with Stable Client Identities

## Objective

Submit authorized paper Order intents exactly once at the broker identity boundary.

## Context

Use [Implementation Plan — Stage 6](../../implementation-plan.md#8-stage-6-paper-order-execution), [Domain Model — Order](../../domain.md#91-order-aggregate), and [Data Model — Unit of Work and Transactions](../../data-model.md#13-unit-of-work-and-transactions).

## Scope

- Validate current Order, account reconciliation, connection state, instrument mapping, capabilities, submission work identity, and paper environment before broker I/O.
- Submit the immutable normalized command with its stable client order ID and persist accepted, rejected, transient, and unknown normalized outcomes.
- Correlate broker order identity and emitted events without overwriting immutable attempts.
- Make retries use the original command and client identity, with bounded attempts and stable terminal codes.
- Audit command hash, adapter identity, environment, timing, result, and redacted diagnostic data.

## Acceptance Criteria

- Exact outbox retries cannot create a second broker Order.
- Accepted submissions bind one broker order identity to the Order.
- Rejected submissions produce a durable terminal Order outcome and release workflow.
- Unknown outcomes enter reconciliation and cannot immediately resubmit.
- Disabled, restricted, unreconciled, stale, incapable, or non-paper inputs fail before broker submission.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=OrderSubmission"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=PaperSubmission"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented the outbox-driven paper submission boundary with durable authorization revalidation, stable client order
identities, bounded broker calls outside database transactions, normalized accepted/rejected/unknown/retryable/
terminal/duplicate handling, and atomic Order transition, immutable submission-attempt audit, and claimed-outbox
completion. Added migration `20260822054340_AddBrokerSubmissionAudit`, deterministic Engine tests for every outcome,
timeout, cancellation, transport ambiguity, preflight rejection, and duplicate dispatch, a SQLite atomicity failpoint,
and a simulated-broker integration proving recovered duplicate dispatch creates one broker Order. Updated architecture
and data-model documentation. No deviation from scope; no ADR or follow-up task was required.

Validation on 2026-08-22:

- `./dev.ps1 build` — passed, zero warnings and errors.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=OrderSubmission"` — 10 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=OrderSubmission"` — 2 passed.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=PaperSubmission"` — 1 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Migrations"` — 3 passed.
- `./dev.ps1 test` — 1,075 passed, 34 intentionally pending Stage 6 acceptance cases, zero failed.
- `./dev.ps1 format` — passed.
- `dotnet ef migrations has-pending-model-changes` inside the repository Docker container — no pending changes.
