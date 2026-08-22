---
schema_version: 1
id: S6-004
title: Add the Stage 6 order execution persistence migration
stage: 6
status: done
priority: 920
type: data
depends_on: [S6-002]
labels: [ef-core, sqlite, migration, orders]
created: 2026-08-21
updated: 2026-08-21
owner: s6_004
---

# S6-004: Add the Stage 6 Order Execution Persistence Migration

## Objective

Persist broker execution identity, Orders, Fills, inbox/outbox work, and reconciliation history with relational integrity.

## Context

Use [Data Model — Execution Tables](../../data-model.md#10-execution-tables), [Data Model — Infrastructure Tables](../../data-model.md#11-infrastructure-tables), [Data Model — Unit of Work and Transactions](../../data-model.md#13-unit-of-work-and-transactions), and [Local Development — Test Strategy](../../local-development.md#4-test-strategy-locally).

## Scope

- Add Stage 6 EF entities, mappings, exact-decimal converters, UTC precision, concurrency tokens, indexes, check constraints, restrictive foreign keys, and immutability triggers.
- Create broker connection/account/reconciliation, Order, Fill, inbox, and outbox schema required by the Stage 6 authorities.
- Add the restrictive Proposal `order_id` relationship after the Order principal exists.
- Enforce unique client order IDs, broker order identities when known, broker execution identities, inbox source-message identities, and ledger/fill source identities.
- Generate and format the migration; add empty-database and completed-Stage-5 upgrade fixtures plus schema-equivalence and model-drift tests.
- Update data-model documentation with exact implemented names and constraints.

## Acceptance Criteria

- Fresh and Stage 5 upgrade migrations retain all prior durable history and produce equivalent schemas.
- Exact financial values and UTC timestamps round-trip without loss.
- Database constraints reject duplicate identities, cross-account inconsistencies, invalid quantities, mutable audit facts, and destructive cascades.
- Generated migration output passes repository formatting.
- EF reports no pending model changes.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage6Migrations"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=OrderPersistence"
docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

- Added the Stage 6 Order, transition, Fill, broker-reconciliation, inbox, and outbox EF entities and mappings with
  canonical exact-decimal and payload storage, UTC millisecond timestamps, optimistic concurrency, restrictive foreign
  keys, stable identity indexes, relational checks, and application-format version 6.
- Added migrations `20260822034547_AddStage6OrderExecution` and
  `20260822034600_AddStage6ExecutionIntegrityTriggers`. The second ordered migration installs cross-account integrity
  and append-only triggers after SQLite completes the Reservation table rebuild, including restoration of Stage 5
  Reservation immutability triggers.
- Added fresh-database and completed-Stage-5 upgrade equivalence, retained-history, schema, identity, invalid-value,
  cross-account, immutability, exact-value, UTC, concurrency, and model-drift coverage; updated earlier migration
  regression fixtures for the current nine-migration schema.
- Updated `docs/data-model.md` with the implemented names, indexes, canonical payload hashes, relationships, and
  immutability rules.
- Validation: `./dev.ps1 restore`; repeated `./dev.ps1 build` (final result 0 warnings/errors); focused
  `Category=Stage6Migrations` and `Category=OrderPersistence` (4/4); all Data tests (153/153); EF
  `migrations has-pending-model-changes` (none); full suite (1,019 passed, 34 expected pending Stage 6 acceptance cases,
  0 failed); and `./dev.ps1 format` all passed.
- Deviation: integrity triggers are isolated in a second ordered migration because SQLite rebuilds
  `capital_reservations` while adding its restrictive Order foreign key. Follow-up tasks: none. ADRs: none.
