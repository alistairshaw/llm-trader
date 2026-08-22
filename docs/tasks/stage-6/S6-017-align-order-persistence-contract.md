---
schema_version: 1
id: S6-017
title: Align order persistence with the execution contract
stage: 6
status: ready
priority: 950
type: defect
depends_on: [S6-004]
labels: [ef-core, sqlite, orders, financial-integrity]
created: 2026-08-22
updated: 2026-08-22
---

# S6-017: Align Order Persistence with the Execution Contract

## Objective

Make the Stage 6 SQLite schema an exact, unambiguous persistence representation of the S6-002 `Order` aggregate.

## Context

S6-005 inspection established that the S6-004 schema cannot exactly persist or rehydrate the authoritative aggregate: `orders` has no currency or quantity-unit columns, its status tokens differ from `OrderStatus`, and its `TimeInForce` token set differs from the Core enumeration. Repository code must not infer missing financial facts or translate between ambiguous lifecycle vocabularies.

Use [Domain Model — Order](../../domain.md#91-order-aggregate), [Data Model — Execution Tables](../../data-model.md#10-execution-tables), [Architecture — Persistence Design](../../architecture.md#13-persistence-design), and [Test Plan — Data Integration Tests](../../test-plan.md#6-data-integration-tests).

## Scope

- Persist the Order currency and quantity unit as required bounded columns.
- Use canonical persisted tokens that exactly match every defined Core `OrderStatus` and `TimeInForce` value.
- Align EF entities, configuration, model snapshot, migration path, check constraints, and indexes with the authoritative aggregate.
- Update the Stage 5 upgrade fixture and migration assertions for the aligned schema.
- Add fresh-database and Stage 5-upgrade tests proving exact Order field and enum-token round trips without inferred or defaulted values.
- Update authoritative persistence documentation with the final column and token definitions.

## Acceptance Criteria

- Every Order financial field required by the S6-002 aggregate has a required, lossless SQLite representation.
- Every defined `OrderStatus` and `TimeInForce` value has one identical canonical database token and every unsupported token is rejected.
- Applying all migrations to an empty database and upgrading the Stage 5 fixture both create the aligned schema.
- Migration and persistence tests prove currency, quantity unit, status, and time-in-force values round-trip exactly.
- EF drift verification passes and no migration path silently invents missing Order data.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage6Migrations"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=PersistenceMappings"
.\dev.ps1 test
.\dev.ps1 format
```

Also run the repository EF migration drift check documented in [Local Development](../../local-development.md).

## Completion Notes

Pending.
