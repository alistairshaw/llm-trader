---
schema_version: 1
id: S3-014
title: Complete Stage 3 runtime acceptance bindings
stage: 3
status: planned
priority: 680
type: acceptance
depends_on: [S3-013]
labels: [bdd, integration, runtime]
created: 2026-08-19
updated: 2026-08-19
---

# S3-014: Complete Stage 3 Runtime Acceptance Bindings

## Objective

Bind and activate every Stage 3 scenario against deterministic application-facing runtime drivers.

## Context

Use the Stage 3 features from `S3-001`, the completed Engine runtime, and [Test Plan — Gherkin Acceptance Tests](../../test-plan.md#10-gherkin-acceptance-tests).

## Scope

- Add thin Stage 3 step definitions and scenario-scoped runtime drivers.
- Compose each scenario with isolated file-backed SQLite, fake UTC clock, deterministic identifiers, scripted model sessions, captured logs, and bounded synchronization gates.
- Exercise application services and the headless host boundary without calling repositories or EF Core from step definitions.
- Activate every Stage 3 feature by removing its temporary pending tag.
- Verify two-Bot concurrency, same-Bot exclusion, trigger coalescing, tool authorization, every budget, safe model failures, requested scheduling, baseline preservation, expired-lease recovery, isolation, complete audit reconstruction, startup, and shutdown.
- Emit deterministic failure diagnostics containing Bot, Run, trigger, lease, configuration, snapshot, tool, budget, and schedule identities.

## Acceptance Criteria

- Every Stage 3 scenario passes with zero pending or skipped result.
- Scenario execution is independent of test order, local time zone, locale, network, credentials, and real elapsed delays.
- Step definitions contain no EF Core, SQLite, provider SDK, or direct persistence implementation call.
- Repeated suite execution produces identical domain outcomes and audit records.
- The complete acceptance suite passes on Windows and Linux.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage3"
.\dev.ps1 test
```

## Completion Notes

Not completed.
