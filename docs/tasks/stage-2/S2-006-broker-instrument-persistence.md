---
schema_version: 1
id: S2-006
title: Persist Broker and Instrument aggregates
stage: 2
status: ready
priority: 820
type: feature
depends_on: [S2-005]
labels: [brokers, instruments, repositories]
created: 2026-08-19
updated: 2026-08-19
---

# S2-006: Persist Broker and Instrument Aggregates

## Objective

Implement faithful persistence for Broker Connections, Broker Accounts, Instruments, and effective-time Broker Mappings.

## Context

Follow [Domain Model — Broker Integration](../../domain.md#6-broker-integration) and [Data Model — Broker Integration Tables](../../data-model.md#7-broker-integration-tables).

## Scope

- Add persistence entities and `IEntityTypeConfiguration<T>` mappings for the four aggregate types.
- Implement repository contracts with explicit aggregate reconstruction and version-aware writes.
- Persist paper/live environment, credential references, capabilities, reconciliation state, precision, lifecycle status, and mapping effective intervals.
- Enforce external account identity and external instrument mapping uniqueness.
- Enforce non-overlapping effective mapping intervals transactionally.
- Add round-trip, uniqueness, concurrency-version, and restricted-delete integration tests.

## Acceptance Criteria

- Every aggregate reloads with value-equivalent domain state.
- Credentials remain references and no secret value enters the database or test artifacts.
- Duplicate scoped external identities are rejected with a purpose-built result.
- Overlapping Instrument Broker Mapping intervals are rejected.
- Paper and live Broker Connections remain explicitly distinguishable after reload.
- Repository APIs expose only domain aggregates and application results.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=BrokerInstrumentPersistence"
.\dev.ps1 build
```

## Completion Notes

Not completed.
