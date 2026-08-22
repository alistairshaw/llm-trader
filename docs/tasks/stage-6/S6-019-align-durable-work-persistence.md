---
schema_version: 1
id: S6-019
title: Align durable broker-work persistence
stage: 6
status: ready
priority: 940
type: defect
depends_on: [S6-018]
labels: [ef-core, sqlite, inbox, outbox, leases]
created: 2026-08-22
updated: 2026-08-22
---

# S6-019: Align Durable Broker-Work Persistence

## Objective

Persist every S6-002 inbox, outbox, and work-envelope fact required for exact durable claims, retries, and recovery.

## Context

The current Stage 6 infrastructure tables omit required envelope and lease facts. `outbox_messages` cannot retain
the explicit work idempotency or correlation identity, and neither table can represent lease ownership and expiry.
`inbox_messages` also cannot represent retry availability or attempt count. Repository code must not overload error,
aggregate, received, or completion columns with unrelated facts.

Use [Architecture — Persistence Design](../../architecture.md#13-persistence-design), [Data Model — Infrastructure Tables](../../data-model.md#11-infrastructure-tables), and [Test Plan — Reliability and Recovery Tests](../../test-plan.md#13-reliability-and-recovery-tests).

## Scope

- Persist the exact Order link, work kind, idempotency key, canonical payload and hash, correlation identity, attempt count, availability, creation or receipt timestamp, status, lease owner and expiry, bounded error/result, and completion timestamp wherever applicable.
- Define canonical required/optional constraints, bounded lengths, status and work-kind tokens, UTC timestamp relationships, payload hashes, unique source/work identities, ownership relationships, and deterministic claim indexes.
- Align EF entities, configuration, model snapshot, forward migration path, and SQLite integrity triggers.
- Restore every affected immutability and ownership trigger after table rebuilds.
- Update fresh-database and Stage 5 upgrade fixtures and assertions.
- Add exact envelope round-trip, atomic competing-claim, lease-expiry, retry scheduling, stale-claim recovery, idempotency, and EF-drift tests.

## Acceptance Criteria

- Every S6-002 `OrderWorkEnvelope` and `BrokerInboxEnvelope` fact round-trips without inference or column overloading.
- Two concurrent claimers cannot own the same work item, and unclaimed eligible work remains available.
- Active leases exclude competitors; expired leases recover deterministically; retry scheduling respects `available_at`.
- Duplicate work and broker source identities return stable existing outcomes.
- Fresh database creation and Stage 5 fixture upgrade both produce the complete aligned schema and indexes.
- EF migration drift verification passes and all affected integrity triggers remain installed.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage6Migrations"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=DurableBrokerWork"
.\dev.ps1 test
.\dev.ps1 format
```

Also run the repository EF migration drift check documented in [Local Development](../../local-development.md).

## Completion Notes

Pending.
