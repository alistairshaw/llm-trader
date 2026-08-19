---
schema_version: 1
id: S3-001
title: Write Stage 3 executable Gherkin specifications
stage: 3
status: ready
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

Not completed.
