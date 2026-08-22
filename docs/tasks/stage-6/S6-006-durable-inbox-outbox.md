---
schema_version: 1
id: S6-006
title: Implement durable broker inbox and outbox processing
stage: 6
status: ready
priority: 880
type: feature
depends_on: [S6-003, S6-005]
labels: [inbox, outbox, broker, idempotency]
created: 2026-08-21
updated: 2026-08-21
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

Pending.
