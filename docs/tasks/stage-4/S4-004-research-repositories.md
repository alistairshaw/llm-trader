---
schema_version: 1
id: S4-004
title: Implement Research repositories and authorized catalog
stage: 4
status: done
priority: 890
type: feature
depends_on: [S4-003]
labels: [research, repositories, catalog, authorization]
created: 2026-08-20
updated: 2026-08-20
owner: s4_004
---

# S4-004: Implement Research Repositories and Authorized Catalog

## Objective

Provide aggregate persistence, concurrency operations, and visibility-safe report discovery through application-owned contracts.

## Context

Use [Domain Model — Repository Boundaries](../../domain.md#12-repository-boundaries), [Data Model — Repository Contracts](../../data-model.md#12-repository-contracts), [Research Bot — Shared Service Model](../../research-bot.md#3-shared-service-model), and [Test Plan — Data Integration Tests](../../test-plan.md#6-data-integration-tests).

## Scope

- Implement Research request and report repositories, atomic queued-request and attempt claims, append-only tool/source audit operations, and optimistic concurrency translation.
- Implement no-tracking catalog search, exact-version retrieval, freshness projection, and visibility authorization for Shared, BotPrivate, and Restricted reports.
- Add deterministic query ordering and validate important catalog plans with SQLite query-plan assertions.
- Reconstruct complete domain aggregates without exposing EF entities, `DbSet`, or `IQueryable`.

## Acceptance Criteria

- Repository round trips preserve all aggregate state, immutable facts, versions, and canonical hashes.
- Concurrent claims permit one active attempt for a request and stale writes return an application concurrency result.
- Unauthorized private and restricted reports are absent from search results and exact-version reads.
- Catalog reads are no-tracking, deterministically ordered, and use the specified Research indexes.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ResearchRepositories|Category=ResearchCatalog"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

- Added application-owned Research request, attempt, report, audit, claim-result, and catalog-query contracts, plus explicit aggregate rehydration state that does not expose EF types or queryables.
- Implemented real-SQLite repositories for Research request/subscription round trips, atomic queued-request and attempt claims, optimistic concurrency translation, append-only ordered tool audit, immutable report/source publication, and complete aggregate reconstruction.
- Added a no-tracking, deterministically ordered catalog with fresh-only search, normalized-key and subject filters, exact report/version retrieval, and deterministic administrator, shared, Bot-private, and restricted-group authorization before pagination.
- Added four repository/catalog integration tests covering round trips, atomic single claims, stale writes, append-only audit, uniqueness rollback, immutable report facts, exact versions, freshness, visibility isolation, no tracking, and the `IX_research_reports_subject_id_generated_at` SQLite query plan. Updated the Research aggregate test fixture for explicit restricted-group scope.
- Validation: `\.\dev.ps1 build` passed with 0 warnings and 0 errors; focused `ResearchRepositories|ResearchCatalog` Data tests passed 4/4; affected Research aggregate tests passed 12/12; all Data tests passed 115/115; Research tests passed 7/7; architecture tests passed 15/15; the full locally applicable suite passed 692 with the 39 planned Stage 4 acceptance scenarios pending; `\.\dev.ps1 format` passed; `dotnet ef migrations has-pending-model-changes` in the development container reported no changes.
- Documentation: clarified the canonical restricted-group request envelope and authorization-before-pagination rule in the Data Model; recorded the generated-migration formatting requirement in `AGENTS.md`. The README remains accurate and required no change.
- Deviations: formatting verification exposed unformatted S4-003 generated migration/entity output, so the repository formatter was applied to those already-committed Stage 4 persistence files as a required gate repair; behavior and schema are unchanged. Follow-ups: none. ADRs: none.
