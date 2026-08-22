---
schema_version: 1
id: S6-017
title: Align order persistence with the execution contract
stage: 6
status: done
priority: 950
type: defect
depends_on: [S6-004]
labels: [ef-core, sqlite, orders, financial-integrity]
created: 2026-08-22
updated: 2026-08-22
owner: s6_017
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

- Added required `currency` and `quantity_unit` Order columns, bounded them to the exact Core value contracts, and
  mapped `OrderStatus`, transition statuses, and `TimeInForce` through canonical enum converters with exhaustive
  SQLite token constraints.
- Added forward migrations `20260822040649_AlignOrderPersistenceContract` and
  `20260822041123_RestoreAlignedOrderIntegrityTriggers`. The schema migration introduces no semantic defaults, so an
  incomplete-schema Order cannot acquire invented financial facts; the immediately following migration restores every
  trigger attached to or referring to the SQLite-rebuilt tables, including immutability of the new fields.
- Extended fresh and completed-Stage-5 upgrade, schema equivalence, exact field/token round-trip, exhaustive Core token,
  invalid constraint, trigger, migration-count, and model-drift coverage. Bounded Core quantity units at 32 lowercase
  ASCII characters so every valid aggregate has a lossless schema representation.
- Updated `AGENTS.md` and `docs/data-model.md` with the exact durable fields/tokens and the SQLite rebuild/trigger
  migration rule.
- Validation: `./dev.ps1 restore`; repeated `./dev.ps1 build` (final 0 warnings/errors); focused
  `Category=Stage6Migrations` (5/5), `Category=PersistenceMappings` (5/5), Core `Category=OrderExecution` (9/9), and
  Core financial-value tests (10/10); all Data tests (154/154); full suite (1,035 passed, 34 expected temporarily
  pending Stage 6 acceptance cases, 0 failed); `./dev.ps1 format`; and EF pending-model-change verification all passed.
- Deviations: trigger restoration is a second ordered migration because EF's SQLite provider defers table rebuilds
  until after raw SQL in the same migration. Follow-up tasks: none. ADRs: none.
