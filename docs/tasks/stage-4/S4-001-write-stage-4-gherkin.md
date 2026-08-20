---
schema_version: 1
id: S4-001
title: Write Stage 4 executable Gherkin specifications
stage: 4
status: ready
priority: 1000
type: acceptance
depends_on: []
labels: [bdd, research, planning]
created: 2026-08-20
updated: 2026-08-20
---

# S4-001: Write Stage 4 Executable Gherkin Specifications

## Objective

Define executable business specifications for every Stage 4 shared-Research acceptance criterion.

## Context

Use [Implementation Plan — Stage 4](../../implementation-plan.md#6-stage-4-shared-research-bot), [Research Bot](../../research-bot.md), [Trading Bot — Research tools](../../trading-bot.md#84-research-tools), [Architecture](../../architecture.md), [Data Model](../../data-model.md#8-research-tables), and [Test Plan](../../test-plan.md#10-gherkin-acceptance-tests).

## Scope

- Add tagged Research features for bounded request validation, authorization, equivalent concurrent deduplication, fresh reuse, visibility, immutable publication, refresh/versioning, provenance, failed-run audit, prompt-injection resistance, tool authority, durable success/failure notification, completion-triggered Bot runs, and restart recovery.
- Specify the two-Bot shared-report demonstration using deterministic fixture-backed sources and a scripted model.
- Add traceability from every Stage 4 criterion to named scenarios and implementing tasks.
- Generate discoverable Reqnroll tests and mark implementation-dependent scenarios with the temporary pending tag used by the acceptance harness; `S4-014` activates them.

## Acceptance Criteria

- Every Stage 4 criterion has explicit business-facing scenario coverage.
- Scenario language identifies request, subscriber, report version, visibility, freshness, source provenance, Research run, notification, and triggered Bot outcomes.
- Tags identify Stage 4, Research, recovery, and applicable platforms.
- The Stage 4 filter discovers every scenario with implementation-dependent scenarios explicitly pending.
- Scenarios use no real LLM, public web, live market data, broker, credential, or wall-clock dependency.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage4"
```

## Completion Notes

Pending implementation.
