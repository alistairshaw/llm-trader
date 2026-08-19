---
schema_version: 1
id: S3-001
title: Write Stage 3 executable Gherkin specifications
stage: 3
status: done
priority: 1000
type: acceptance
depends_on: []
labels: [bdd, runtime, scheduling, multi-bot]
created: 2026-08-19
updated: 2026-08-19
---

# S3-001: Write Stage 3 Executable Gherkin Specifications

## Objective

Define executable business specifications for every Stage 3 runtime and scheduling acceptance criterion.

## Context

Use [Implementation Plan — Stage 3](../../implementation-plan.md#5-stage-3-multi-bot-runtime-and-scheduling), [Trading Bot](../../trading-bot.md), [Architecture](../../architecture.md), [Data Model](../../data-model.md), and [Test Plan](../../test-plan.md).

## Scope

- Add tagged Stage 3 feature files for manual and scheduled runs, lease exclusivity, cross-bot concurrency, trigger retention and coalescing, pinned configuration and snapshot input, tool authorization, every run budget, model failure, `Finish`, requested scheduling, baseline scheduling, restart recovery, isolation, audit reconstruction, headless startup, and graceful shutdown.
- Express outcomes through application-facing drivers and deterministic synthetic inputs.
- Add traceability from every Stage 3 acceptance criterion to scenarios and implementing tasks.
- Generate discoverable Reqnroll/NUnit tests and document the Stage 3 filter command.
- Mark implementation-dependent scenarios with the temporary Stage 3 pending tag used by the acceptance harness; `S3-014` activates every scenario.

## Acceptance Criteria

- Every Stage 3 criterion has explicit scenario coverage.
- Scenario language names the Bot, Portfolio, configuration, snapshot, trigger, lease, budget, tool, and schedule decisions involved.
- Tags identify Stage 3, runtime, scheduling, recovery, and platform requirements.
- The Stage 3 filter discovers every scenario and reports implementation-dependent scenarios as explicitly pending.
- Scenarios contain no external model, web, market-data, broker, or wall-clock dependency.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage3"
```

## Completion Notes

Completed on 2026-08-19.

- Added six Stage 3 feature files containing 26 unique deterministic Reqnroll test cases: 20 scenarios plus six examples covering every run-budget dimension.
- Specified manual and scheduled runs, exclusive leases, cross-Bot concurrency and capacity, durable trigger coalescing, pinned configuration and snapshot input, `GetPortfolioSnapshot` and `Finish`, tool authorization, all six budgets, malformed responses, missing `Finish`, requested and baseline scheduling, restart recovery, ownership isolation, audit reconstruction, headless startup, and graceful shutdown.
- Tagged every scenario for Stage 3 acceptance, runtime, and cross-platform execution; scheduling and recovery scenarios also carry their respective tags. Applied the acceptance harness's temporary `@ignore` tag so implementation-dependent tests remain explicitly pending until `S3-014` binds and activates them.
- Added a traceability matrix mapping every Stage 3 acceptance criterion and explicit runtime deliverable to named scenarios and implementing tasks, including the documented Stage 3 filter command.
- Generated and committed the Reqnroll/NUnit test cases for discoverability.
- Validation: `.\dev.ps1 build` passed with 0 warnings and 0 errors. `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage3"` discovered 26 tests and reported all 26 explicitly skipped/pending, with 0 failures.
- Deviations: none. Follow-up tasks: none. ADRs: none.
