---
schema_version: 1
id: S4-003
title: Add the Stage 4 Research persistence migration
stage: 4
status: ready
priority: 910
type: infrastructure
depends_on: [S4-002]
labels: [research, ef-core, sqlite, migration]
created: 2026-08-20
updated: 2026-08-20
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

Pending implementation.
