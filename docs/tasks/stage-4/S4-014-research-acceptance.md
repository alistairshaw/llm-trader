---
schema_version: 1
id: S4-014
title: Complete Stage 4 Research acceptance bindings
stage: 4
status: ready
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

Pending implementation.
