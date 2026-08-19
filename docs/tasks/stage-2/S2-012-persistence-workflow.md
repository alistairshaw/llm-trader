---
schema_version: 1
id: S2-012
title: Implement the restart-safe portfolio persistence workflow
stage: 2
status: ready
priority: 720
type: feature
depends_on: [S2-010, S2-011]
labels: [integration, restart, acceptance]
created: 2026-08-19
updated: 2026-08-19
---

# S2-012: Implement the Restart-Safe Portfolio Persistence Workflow

## Objective

Demonstrate the complete Stage 2 persistence slice through application-facing services and a process restart boundary.

## Context

Implement the [Stage 2 demonstration](../../implementation-plan.md#4-stage-2-persistence-and-portfolio-state) using the architecture’s application-service and repository boundaries.

## Scope

- Add application services that create a paper Broker Connection, Broker Account, Instrument and mapping, Trading Bot and configuration, Portfolio assignment, ledger funding entries, Positions, and a Decision Snapshot.
- Add `Trading.IntegrationTests` as a `net10.0` NUnit project in the solution with the standard shared test conventions and locked dependencies.
- Commit changes through repository contracts and `IUnitOfWork`.
- Add an integration driver that disposes the first service provider, creates a new provider against the same temporary SQLite database, and reloads the complete portfolio state.
- Bind every Stage 2 Gherkin scenario to application-facing drivers.
- Assert exact financial state, identities, relationships, timestamps, versions, hashes, and immutable history after restart.
- Add failure-path scenarios for ownership conflicts, duplicate ledger sources, stale concurrency, restricted deletes, migration upgrade, and transaction rollback.

## Acceptance Criteria

- The persisted portfolio reloads identically after a complete host/service-provider restart.
- Every Stage 2 scenario executes without pending or skipped status.
- Scenario steps use application services and query services rather than EF Core directly.
- Each scenario uses a unique temporary SQLite database and deterministic clock and identities.
- Captured diagnostics identify the database, migration, aggregate, and operation on failure without exposing secrets.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.IntegrationTests
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage2"
.\dev.ps1 test
```

## Completion Notes

Not completed.
