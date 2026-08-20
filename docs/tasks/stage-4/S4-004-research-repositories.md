---
schema_version: 1
id: S4-004
title: Implement Research repositories and authorized catalog
stage: 4
status: planned
priority: 890
type: feature
depends_on: [S4-003]
labels: [research, repositories, catalog, authorization]
created: 2026-08-20
updated: 2026-08-20
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

Pending implementation.
