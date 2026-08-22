---
schema_version: 1
id: S6-009
title: Reconcile unknown order submission outcomes
stage: 6
status: done
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

Implemented durable unknown-submission reconciliation by stable paper account, environment, and client order identity.
Unknown submission completion now atomically queues reconciliation; broker lookup occurs after the durable claim commits
and outside database transactions. Found outcomes bind broker identity and normalized Order status atomically with
append-only canonical audit. Absence requires a deterministic grace period and repeated authoritative lookup before the
original uniquely keyed submission work is reactivated with its unchanged client identity. Ambiguity, outage, mismatch,
attempt exhaustion, cancellation, contention, and restart retries retain stable safe outcomes without direct resubmission.

Validation completed in the Linux development container:

- `.\dev.ps1 build` — passed with zero warnings and errors.
- `.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=SubmissionReconciliation"` — 5 passed.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=SubmissionReconciliation"` — 1 passed.
- `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=UnknownSubmission"` — 1 passed.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage6Migrations"` — 9 passed.
- `.\dev.ps1 test` — 1,082 passed, 34 intentionally pending later Stage 6 acceptance cases, zero failures.
- `.\dev.ps1 format` — passed after applying the repository formatter, including one pre-existing indentation defect in
  `InitialMigrationTests` required for the formatting gate.

No migrations, ADRs, deviations, or follow-up tasks were required. Hosted Windows validation remains delegated to CI.
