---
schema_version: 1
id: S2-001
title: Write Stage 2 executable Gherkin specifications
stage: 2
status: done
priority: 1000
type: acceptance
depends_on: []
labels: [bdd, persistence, portfolio]
created: 2026-08-19
updated: 2026-08-19
---

# S2-001: Write Stage 2 Executable Gherkin Specifications

## Objective

Define executable business specifications for every Stage 2 persistence and portfolio-state acceptance criterion.

## Context

Use [Implementation Plan — Stage 2](../../implementation-plan.md#4-stage-2-persistence-and-portfolio-state), [Domain Model](../../domain.md), [Data Model](../../data-model.md), and [Test Plan](../../test-plan.md) as authoritative behavior.

## Scope

- Add tagged Stage 2 feature files for aggregate round trips, exact financial storage, UTC timestamps, identifier conversion, ownership constraints, ledger idempotency and corrections, immutable decision snapshots, optimistic concurrency, no-tracking projections, migrations, delete restrictions, restart recovery, and transaction rollback.
- Express observable domain outcomes through application-facing drivers.
- Add traceability from every Stage 2 acceptance criterion to at least one scenario.
- Generate discoverable Reqnroll/NUnit test cases and document the Stage 2 filter command.
- Mark implementation-dependent scenarios with the temporary Stage 2 pending tag used by the existing acceptance harness, with `S2-012` responsible for binding and activating every scenario.

## Acceptance Criteria

- Every Stage 2 criterion has explicit scenario coverage.
- Scenarios use domain language and deterministic synthetic data.
- Scenario tags identify Stage 2, persistence, migration, and platform requirements.
- Generated tests are discoverable through the repository test command.
- The Stage 2 filter discovers every scenario and reports each implementation-dependent scenario as explicitly pending.
- The feature suite contains no undefined requirement or contradictory expected outcome.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage2"
```

## Completion Notes

Completed on 2026-08-19.

- Added seven Stage 2 feature files containing 20 unique, deterministic scenarios for aggregate round trips, exact decimals, UTC timestamps, typed identifiers, ownership constraints, ledger idempotency and corrections, immutable snapshots, optimistic concurrency, rollback, repository boundaries, no-tracking projections, real SQLite use, migrations, and restricted deletion.
- Tagged every scenario for Stage 2 acceptance, persistence, and cross-platform execution; migration scenarios also carry `@migration`. Applied the acceptance harness's temporary `@ignore` tag so all implementation-dependent tests are explicitly pending until `S2-012` binds and activates them.
- Added a traceability matrix mapping every Stage 2 acceptance criterion to named scenarios and implementing tasks, with the documented Stage 2 filter command.
- Generated and committed the Reqnroll/NUnit test cases for discoverability.
- Validation: `.\dev.ps1 build` passed with 0 warnings and 0 errors. `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage2"` discovered 20 tests and reported all 20 explicitly skipped/pending, with 0 failures.
- Deviations: none. Follow-up tasks: none. ADRs: none.
