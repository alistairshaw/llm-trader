---
schema_version: 1
id: S6-008
title: Submit paper orders with stable client identities
stage: 6
status: planned
priority: 840
type: feature
depends_on: [S6-006, S6-007]
labels: [submission, idempotency, client-order-id, paper]
created: 2026-08-21
updated: 2026-08-21
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

Pending.
