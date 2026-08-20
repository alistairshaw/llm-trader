---
schema_version: 1
id: S5-003
title: Add the Stage 5 proposal persistence migration
stage: 5
status: done
priority: 920
type: infrastructure
depends_on: [S5-002]
labels: [ef-core, sqlite, migration, proposals]
created: 2026-08-20
updated: 2026-08-20
owner: s5_003
---

# S5-003: Add the Stage 5 Proposal Persistence Migration

## Objective

Persist proposals, exact evidence, immutable evaluations, human decisions, and capital reservations in SQLite.

## Context

Use [Data Model — Trade Proposal Tables](../../data-model.md#9-trade-proposal-tables), [Data Model — EF Core Mapping Rules](../../data-model.md#17-ef-core-mapping-rules), and [Test Plan — Migration Tests](../../test-plan.md#7-migration-tests).

## Scope

- Add EF entities and configurations for hypotheses, hypothesis versions, hypothesis evidence/test results, proposals, proposal evidence reports, evaluations, approvals, and reservations specified by the data model.
- Add exact decimals, UTC conversions, canonical versioned JSON, concurrency tokens, foreign keys, delete restrictions, check constraints, and unique/query indexes.
- Add the Stage 5 migration and a completed Stage 4 upgrade fixture with representative portfolio, Bot Run, Research report, and notification history.
- Enforce frozen Hypothesis versions and immutable proposal content, evidence, evaluation, approval, and terminal reservation facts through relational constraints and SQLite safeguards.
- Format generated migration output and update data-model documentation when implementation details make it inaccurate.

## Acceptance Criteria

- Fresh migration and Stage 4 fixture upgrade produce equivalent Stage 5 schemas while retaining exact prior-stage history.
- SQLite enforces Hypothesis version uniqueness, proposal idempotency, evaluation sequence, exact evidence uniqueness, one active reservation per proposal, valid states, and restricted deletes.
- Exact decimals, UTC timestamps, strongly typed IDs, canonical payloads, and concurrency tokens round-trip through real SQLite.
- EF model drift is empty and persistence types remain internal to `Trading.Data`.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage5Migrations|Category=ProposalPersistence"
.\dev.ps1 test
.\dev.ps1 format
docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"
```

## Completion Notes

Implemented migration `20260820211945_AddStage5ProposalGovernance` with nine Stage 5 governance tables, restrictive relationships, canonical JSON/hash and exact-decimal storage, lifecycle checks, concurrency tokens, query/uniqueness indexes, and SQLite immutability triggers. Added a representative completed-Stage-4 upgrade path and real-SQLite coverage for fresh/upgrade schema equivalence, retained history, relational constraints, immutable audit facts, exact values, UTC millisecond timestamps, concurrency, and model drift. Updated the data-model documentation to record the Stage 6 timing of the reservation-to-order foreign key and updated the README with the durable Stage 5 foundation.

Validation completed on 2026-08-20: `./dev.ps1 build` passed with zero warnings and errors; the focused Stage 5 migration/persistence tests passed 5/5; `./dev.ps1 test -Project tests/Trading.Data.Tests` passed 135/135; `./dev.ps1 test` passed 898 tests with the 32 intentionally pending S5-001 scenarios and no failures; `./dev.ps1 format` passed; and `dotnet ef migrations has-pending-model-changes --project src/Trading.Data` reported no changes inside the Docker development container.

Deviation: the nullable `capital_reservations.order_id` column is created now, while its restrictive foreign key is intentionally added by the Stage 6 migration when the `orders` principal table exists. No follow-up tasks or ADRs were created.
