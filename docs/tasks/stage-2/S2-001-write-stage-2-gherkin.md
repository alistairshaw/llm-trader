---
schema_version: 1
id: S2-001
title: Write Stage 2 executable Gherkin specifications
stage: 2
status: ready
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

Not completed.
