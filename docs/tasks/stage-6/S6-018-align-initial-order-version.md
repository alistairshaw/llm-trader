---
schema_version: 1
id: S6-018
title: Align the initial Order concurrency version
stage: 6
status: done
priority: 945
type: defect
depends_on: [S6-017]
labels: [ef-core, sqlite, orders, concurrency]
created: 2026-08-22
updated: 2026-08-22
owner: s6_018
---

# S6-018: Align the Initial Order Concurrency Version

## Objective

Permit the authoritative initial `Order.Version` value while preserving optimistic, monotonic concurrency updates.

## Context

A newly constructed S6-002 Core `Order` has status `Created`, no transitions, and `Version == 0`. The Stage 6
database currently requires `orders.version > 0`, so the initial aggregate required by atomic proposal conversion
cannot be persisted exactly. Repository code must not invent a transition or increment solely to satisfy storage.

Use [Domain Model — Order](../../domain.md#91-order-aggregate), [Data Model — Concurrency](../../data-model.md#132-concurrency), and [Test Plan — Data Integration Tests](../../test-plan.md#6-data-integration-tests).

## Scope

- Permit `orders.version == 0` for the authoritative initial aggregate and reject negative values.
- Preserve application-maintained optimistic concurrency and strictly monotonic updates after transitions.
- Align EF configuration, model snapshot, forward migration path, and order integrity constraints.
- Restore every affected SQLite immutability and ownership trigger after any table rebuild.
- Update fresh-database and Stage 5 upgrade fixtures and assertions.
- Add exact initial-Order round-trip and stale-writer concurrency tests.

## Acceptance Criteria

- A new `Created` Order with zero transitions and `Version == 0` persists and round-trips exactly.
- Negative versions are rejected by SQLite.
- Each valid aggregate transition advances the version monotonically, and a stale expected version returns a stable concurrency outcome.
- Fresh database creation and Stage 5 fixture upgrade both produce the aligned constraint.
- All affected integrity triggers remain installed after migration.
- EF migration drift verification passes.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=OrderExecution"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage6Migrations"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=PersistenceMappings"
.\dev.ps1 test
.\dev.ps1 format
```

Also run the repository EF migration drift check documented in [Local Development](../../local-development.md).

## Completion Notes

Implemented `orders.version >= 0` through forward migrations
`20260822042907_AlignInitialOrderVersion` and
`20260822043030_RestoreInitialOrderVersionTriggers`. The first migration explicitly drops every trigger attached to
or referring to the rebuilt `orders` table; the immediately following migration restores them and adds a monotonic
version trigger. EF configuration, snapshot, migration fixtures, and the data-model concurrency contract now match the
Core aggregate.

Validation completed on 2026-08-22:

- `./dev.ps1 build` — passed with zero warnings and errors.
- `./dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=OrderExecution"` — 9 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage6Migrations"` — 5 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=PersistenceMappings"` — 5 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests` — 154 passed.
- `./dev.ps1 test` — 1,035 passed, 34 expected pending Stage 6 acceptance scenarios, zero failures.
- `./dev.ps1 format` — passed.
- `dotnet ef migrations has-pending-model-changes` inside the development container — no pending model changes.
- Generated SQL from the previous trigger-restoration migration through the new pair — verified trigger drops precede
  the `orders` rebuild and all affected triggers are recreated afterward.

No scope deviations, follow-up tasks, or ADRs.
