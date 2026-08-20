---
schema_version: 1
id: S4-016
title: Bind Stage 4 acceptance to production Research workflows
stage: 4
status: ready
priority: 1100
type: defect
depends_on: [S4-013]
labels: [bdd, research, integration, stage-gate]
created: 2026-08-20
updated: 2026-08-20
---

# S4-016: Bind Stage 4 Acceptance to Production Research Workflows

## Objective

Make every Stage 4 scenario prove its observable outcome through production Research, Engine, Data, notification, recovery, and host services.

## Context

Use [Test Plan — Gherkin Acceptance Tests](../../test-plan.md#10-gherkin-acceptance-tests), [Steps and Drivers](../../test-plan.md#103-steps-and-drivers), [Implementation Plan — Stage 4](../../implementation-plan.md#6-stage-4-shared-research-bot), and [Stage 4 traceability](../../../tests/Trading.AcceptanceTests/Features/Research/TRACEABILITY.md).

The Stage 4 review found that the current driver derives outcomes from Gherkin text and scenario titles. `S4-015` requires production-backed evidence before the stage can close.

## Scope

- Replace keyword-derived Stage 4 scenario outcomes with scenario-scoped calls to production application services and observable query results.
- Compose the production Research request, catalog, tool dispatch, bounded model loop, publication, notification, Trading Bot Research tool, orchestration, recovery, and host services required by the Stage 4 scenarios.
- Give every scenario a fresh migrated SQLite file, fixed clock, deterministic identifiers, scripted model responses, approved fixture sources, and captured runtime diagnostics.
- Seed scenario state through production repositories only inside the driver’s composition and fixture boundary when no public application command creates the required prerequisite.
- Assert durable request, attempt, tool audit, source provenance, immutable report version, subscription, notification, trigger, and Bot Run facts from production read paths or persistence inspection owned by the driver.
- Retain thin feature steps that route Stage 4 vocabulary to the driver.
- Keep every Stage 4 test deterministic, isolated, cross-platform, credential-free, and network-free.
- Update Stage 4 traceability and repository guidance to describe the resulting production-backed acceptance boundary.

## Acceptance Criteria

- No Stage 4 pass/fail outcome is derived from matching feature text, scenario titles, or preassigned expected values.
- Every Stage 4 action invokes a production application workflow and every outcome is asserted from returned application results or durable state produced by that workflow.
- The 39 Stage 4 scenarios prove request authorization, deduplication, reuse, visibility, publication, versioning, provenance, injection containment, bounded execution, notification, trigger delivery, recovery, exact-version Trading Bot consumption, and headless behavior.
- Scenario diagnostics include stable request, attempt, report, source, subscription, notification, trigger, and Bot Run identities when those artifacts exist.
- Two consecutive focused Stage 4 runs pass 39 tests with zero failed, pending, or skipped results and identical canonical business hashes.
- The complete locally applicable suite passes with zero failed or skipped tests.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage4"
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage4"
.\dev.ps1 test -Project tests/Trading.Research.Tests
.\dev.ps1 test -Project tests/Trading.Engine.Tests
.\dev.ps1 test -Project tests/Trading.Data.Tests
.\dev.ps1 test -Project tests/Trading.IntegrationTests
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 test
.\dev.ps1 format
docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"
```

Inspect `Stage4ResearchDriver` and the Stage 4 step definitions to confirm scenario outcomes come from production workflow results and durable facts.

## Completion Notes

Pending implementation.
