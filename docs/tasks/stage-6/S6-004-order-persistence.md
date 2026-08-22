---
schema_version: 1
id: S6-004
title: Add the Stage 6 order execution persistence migration
stage: 6
status: ready
priority: 920
type: data
depends_on: [S6-002]
labels: [ef-core, sqlite, migration, orders]
created: 2026-08-21
updated: 2026-08-21
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

Pending.
