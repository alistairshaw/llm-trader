---
schema_version: 1
id: S6-006
title: Implement durable broker inbox and outbox processing
stage: 6
status: done
priority: 880
type: feature
depends_on: [S6-003, S6-005]
labels: [inbox, outbox, broker, idempotency]
created: 2026-08-21
updated: 2026-08-22
---

# S6-006: Implement Durable Broker Inbox and Outbox Processing

## Objective

Run bounded durable broker work with atomic state transitions and canonical audit payloads.

## Context

Use [Data Model — Infrastructure Tables](../../data-model.md#11-infrastructure-tables), [Data Model — Unit of Work and Transactions](../../data-model.md#13-unit-of-work-and-transactions), and [Test Plan — Orders and Fills](../../test-plan.md#102-initial-journey-catalog).

## Scope

- Implement bounded outbox and inbox workers with conditional claims, lease renewal, backoff, terminal failure, cancellation, and graceful shutdown.
- Execute broker I/O only after claim transactions commit and persist normalized outcomes in subsequent transactions.
- Canonicalize, size-bound, hash, and redact durable commands, messages, results, timings, errors, and correlation identities.
- Deduplicate inbox messages by source and external message identity before dispatch.
- Expose deterministic drain operations for acceptance tests and headless smoke.

## Acceptance Criteria

- Atomic tests prove workers never hold a database transaction across broker I/O.
- Claim contention, transient failure, cancellation, and restart preserve exactly one durable work history.
- Duplicate inbox delivery invokes business processing once.
- Unbounded or malformed payloads terminate with stable redacted outcomes.
- Work remains isolated by broker account and correlation chain.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=DurableBrokerProcessing"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=BrokerInboxOutbox"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented bounded durable outbox and inbox processors with committed conditional claims, lease renewal, capped
exponential retry, stale-lease recovery, cancellation release, terminal poison handling, stable redacted outcomes,
strict canonical JSON validation, and per-item failure containment. Extended durable envelopes with persisted attempt
state and repository contracts with conditional renewal and failure transitions. Changed post-claim state writes to
atomic server-side updates so stale EF tracked state cannot reject the current lease owner.

Validation completed in the Linux Docker workflow on 2026-08-22:

- `./dev.ps1 build` — passed, zero warnings and zero errors.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=DurableBrokerProcessing"` — 8 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=DurableBrokerWork"` — 7 passed.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=BrokerInboxOutbox"` — 2 passed.
- `./dev.ps1 test` — 1,052 passed, zero failed, 34 expected pending Stage 6 acceptance cases.
- `./dev.ps1 format` — passed.
- `dotnet ef migrations has-pending-model-changes` through Docker Compose — no pending model changes.

Updated `data-model.md` with durable worker transaction and failure semantics and `AGENTS.md` with the conditional
server-update rule discovered while validating claims. No deviations, follow-up tasks, or ADRs.
