---
schema_version: 1
id: S6-009
title: Reconcile unknown order submission outcomes
stage: 6
status: ready
priority: 820
type: feature
depends_on: [S6-008]
labels: [reconciliation, unknown-outcome, recovery, broker]
created: 2026-08-21
updated: 2026-08-22
---

# S6-009: Reconcile Unknown Order Submission Outcomes

## Objective

Resolve uncertain submission outcomes by stable client identity before permitting another broker action.

## Context

Use [Domain Model — Broker Account](../../domain.md#62-brokeraccount-aggregate), [Domain Model — Order](../../domain.md#91-order-aggregate), and [Data Model — Broker Integration Tables](../../data-model.md#7-broker-integration-tables).

## Scope

- Queue and claim reconciliation after every unknown submission outcome.
- Query the broker by account, environment, and stable client order ID, then normalize found, absent, ambiguous, unavailable, and mismatched results.
- Persist each append-only reconciliation attempt and atomically transition the Order or schedule bounded follow-up work.
- Bind found broker identity and ingest accompanying status/execution facts through the durable inbox.
- Permit resubmission only after authoritative absence and unchanged authorization state; require renewed validation where freshness has expired.

## Acceptance Criteria

- Broker acceptance followed by client timeout resolves to the accepted broker Order without duplicate submission.
- Absence, ambiguity, outage, identity mismatch, and exhausted attempts have stable safe outcomes.
- Reconciliation attempts retain exact client identity, account, environment, timing, normalized snapshot, differences, and resolution.
- No reconciliation transaction spans broker I/O.
- Concurrent reconcilers cannot resolve or retry one Order twice.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=SubmissionReconciliation"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=UnknownSubmission"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending.
