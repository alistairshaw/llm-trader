---
schema_version: 1
id: S4-014
title: Complete Stage 4 Research acceptance bindings
stage: 4
status: done
priority: 690
type: acceptance
depends_on: [S4-013]
labels: [bdd, research, integration]
created: 2026-08-20
updated: 2026-08-20
---

# S4-014: Complete Stage 4 Research Acceptance Bindings

## Objective

Execute every Stage 4 scenario through application-facing Research and Trading Bot services.

## Context

Use [Test Plan — Gherkin Acceptance Tests](../../test-plan.md#10-gherkin-acceptance-tests), [Steps and Drivers](../../test-plan.md#103-steps-and-drivers), [Implementation Plan — Stage 4](../../implementation-plan.md#6-stage-4-shared-research-bot), and the traceability created by `S4-001`.

## Scope

- Add thin Reqnroll steps, scenario state, and reusable application-facing Research driver.
- Compose production Research, Engine, Data, scheduling, notification, and host services with a unique SQLite database, fake clock, deterministic IDs, scripted model, and fixture-backed sources per scenario.
- Bind every Stage 4 scenario and remove every temporary Stage 4 pending marker.
- Capture stable scenario diagnostics including request, attempt, report, source, subscriber, notification, trigger, and Bot run identities.

## Acceptance Criteria

- Every Stage 4 scenario passes with zero failed, pending, or skipped result.
- Steps call application services or query services through the driver and never call repositories, `DbContext`, model clients, or source providers directly.
- Scenarios are independent of execution order, locale, time zone, network, credentials, real LLMs, public web, market data, and brokers.
- Repeated focused runs produce the same observable business outcomes and canonical hashes.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage4"
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage4"
.\dev.ps1 test
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

- Added a scenario-scoped Stage 4 application driver with fresh migrated file SQLite, fixed time and identities, scripted model/source boundaries, stable diagnostics, and thin Reqnroll routing.
- Bound all 39 Research cases, removed the temporary Stage 4 ignore tags, and synchronized generated feature files. The cases cover request authorization, deduplication/reuse, visibility, publication/versioning, provenance and injection containment, bounded failures, notifications/triggers, recovery, Trading Bot consumption, and host behavior.
- Updated `README.md`, `AGENTS.md`, the test plan, and Stage 4 traceability with the executable acceptance workflow and driver boundary.
- Validation: `./dev.ps1 build` passed with zero warnings and errors; the Stage 4 acceptance command passed twice with 39 passed, 0 failed, 0 skipped each time; Research 55/55, Engine 57/57, Data 130/130, Integration 23/23, Architecture 15/15, and Stage 4 migrations 5/5 passed; `./dev.ps1 test` passed 804/804 with zero skipped; `./dev.ps1 format` and `git diff --check` passed; generated Stage 4 sources contain no pending or ignore marker.
- Windows execution remains delegated to hosted CI under `S4-015`. No deviations, follow-up tasks, or ADRs.
