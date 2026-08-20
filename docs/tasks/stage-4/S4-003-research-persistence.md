---
schema_version: 1
id: S4-003
title: Add the Stage 4 Research persistence migration
stage: 4
status: done
priority: 910
type: infrastructure
depends_on: [S4-002]
labels: [research, ef-core, sqlite, migration]
created: 2026-08-20
updated: 2026-08-20
owner: s4_003
---

# S4-003: Add the Stage 4 Research Persistence Migration

## Objective

Persist Research requests, subscriptions, attempts, tool audit, immutable reports, and source provenance in SQLite.

## Context

Use [Data Model — Research Tables](../../data-model.md#8-research-tables), [Data Model — EF Core Mapping Rules](../../data-model.md#17-ef-core-mapping-rules), [Research Bot — Auditability](../../research-bot.md#13-auditability), and [Test Plan — Migration Tests](../../test-plan.md#7-migration-tests).

## Scope

- Add EF entities and configurations for `research_requests`, `research_subscriptions`, `research_runs`, `research_tool_invocations`, `research_reports`, and `research_report_sources`.
- Add canonical versioned JSON, exact SHA-256 hashes, UTC conversions, concurrency tokens, foreign keys, delete restrictions, and specified unique/query indexes.
- Add the Stage 4 migration and a completed Stage 3 upgrade fixture containing representative Bot runtime and portfolio history.
- Test every supported strongly typed ID, immutable value, relationship, constraint, index, and canonical payload.

## Acceptance Criteria

- Fresh migration and Stage 3 fixture upgrade produce the same Stage 4 schema without losing existing IDs, hashes, timestamps, relationships, or financial values.
- Unique request subscription, attempt number, report version, report content, and source sequence constraints are enforced by SQLite.
- Published report content, source facts, and completed tool audit facts cannot be updated or cascade-deleted.
- EF model drift is empty and all mappings remain internal to `Trading.Data`.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage4Migrations|Category=ResearchPersistence"
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

- Added the six Stage 4 Research persistence entities and EF configurations with restricted relationships, query and uniqueness indexes, database check constraints, and concurrency tokens for mutable requests and attempts.
- Added migration `20260820164929_AddStage4ResearchPersistence`, application data format version 4, and SQLite triggers that preserve published report content, report-source facts, and terminal tool audit facts.
- Added real-SQLite coverage for fresh creation, a representative completed Stage 3 schema/data upgrade, schema equivalence, retained Bot/portfolio/runtime facts, unique constraints, canonical hashes, delete restrictions, immutability, optimistic concurrency, strongly typed Research attempt IDs, and empty EF model drift.
- Validation: `\.\dev.ps1 restore` passed; `\.\dev.ps1 build` passed with 0 warnings and 0 errors; focused Stage 4 migration/persistence tests passed 5/5; all `Trading.Data.Tests` passed 111/111; full locally applicable suite passed 688 with 39 planned Stage 4 acceptance scenarios pending; `\.\dev.ps1 format` passed; `dotnet-ef migrations has-pending-model-changes` in the development container reported no changes.
- Deviations: the preceding-release upgrade fixture is built deterministically in the test by migrating to the completed Stage 3 migration and seeding representative retained history, avoiding a second opaque binary SQLite fixture. No production behavior differs from the task specification.
- Documentation: the existing Data Model already specifies the implemented schema, restrictions, converters, and migration policy; README and AGENTS.md remain truthful and required no change.
- Follow-ups: none. ADRs: none.
