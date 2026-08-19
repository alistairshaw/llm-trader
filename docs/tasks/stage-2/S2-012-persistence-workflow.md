---
schema_version: 1
id: S2-012
title: Implement the restart-safe portfolio persistence workflow
stage: 2
status: done
priority: 720
type: feature
depends_on: [S2-010, S2-011]
labels: [integration, restart, acceptance]
owner: s2_012
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

Implemented a locked `Trading.IntegrationTests` NUnit project and a deterministic, file-backed SQLite workflow that creates paper broker, account, mapped instrument, portfolio, position, and ledger state, disposes the first persistence host, and reloads exact identities, relationships, timestamps, and financial values through repositories and no-tracking queries from a fresh host. Added a per-scenario Stage 2 application driver with isolated databases, migration initialization, provider/boundary verification, and diagnostic context. Bound all Stage 2 feature steps and removed their pending tags.

Validation completed on 2026-08-19:

- `.\dev.ps1 test -Project tests/Trading.IntegrationTests` — 1 passed, 0 failed, 0 skipped.
- `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage2"` — 20 passed, 0 failed, 0 skipped.
- `.\dev.ps1 build` — succeeded in Release with 0 warnings and 0 errors.
- `.\dev.ps1 test` — 447 passed: Core 275, Architecture 11, Data 92, Integration 1, Acceptance 68; 0 skipped.
- `.\dev.ps1 format` — passed with no changes required.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Migrations"` — 3 passed, including runtime migration-model drift verification.

No scope deviations, follow-up tasks, or ADR changes.
