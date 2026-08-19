---
schema_version: 1
id: S2-006
title: Persist Broker and Instrument aggregates
stage: 2
status: done
priority: 820
type: feature
depends_on: [S2-005]
labels: [brokers, instruments, repositories]
created: 2026-08-19
updated: 2026-08-19
owner: codex-s2-006
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

Completed 2026-08-19.

- Added explicit reconstruction state for Broker Connections, Broker Accounts, Instruments, and effective-time Instrument Broker Mappings, including capabilities, lifecycle state, reconciliation state, precision, timestamps, and persistence versions.
- Implemented the three aggregate repository contracts with domain-only results, canonical value conversion, version-aware writes, scoped uniqueness translation, transactional mapping replacement, and effective-interval overlap checks.
- Added SQLite integration coverage for aggregate round trips, paper/live distinction, credential-reference storage, scoped external identity conflicts, mapping overlaps, stale versions, and restricted deletes.

Validation:

- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=BrokerInstrumentPersistence"` — passed, 8 tests.
- `.\dev.ps1 build` — passed in Release with 0 warnings and 0 errors.
- `docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"` — passed; no pending model changes.
- `.\dev.ps1 test` — passed: 393 tests; 20 intentionally deferred Stage 2 acceptance scenarios skipped.
- `.\dev.ps1 format` — passed.

Deviations: none.

Follow-up tasks: none.

ADRs: none.
