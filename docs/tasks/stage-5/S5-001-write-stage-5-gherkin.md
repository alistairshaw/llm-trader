---
schema_version: 1
id: S5-001
title: Write Stage 5 executable Gherkin specifications
stage: 5
status: done
priority: 1000
type: acceptance
depends_on: []
labels: [bdd, proposals, risk, planning]
created: 2026-08-20
updated: 2026-08-20
---

# S5-001: Write Stage 5 Executable Gherkin Specifications

## Objective

Define executable business specifications for every Stage 5 proposal-governance acceptance criterion.

## Context

Use [Implementation Plan — Stage 5](../../implementation-plan.md#7-stage-5-trade-proposals-approvals-and-risk), [Domain Model — Trade Proposals](../../domain.md#8-trade-proposals), [Trading Bot — Proposal Validation and Execution](../../trading-bot.md#10-proposal-validation-and-execution), [Data Model — Trade Proposal Tables](../../data-model.md#9-trade-proposal-tables), and [Test Plan — Gherkin Acceptance Tests](../../test-plan.md#10-gherkin-acceptance-tests).

## Scope

- Add tagged features for direct-trade and target-allocation proposals, exact identity and evidence references, schema validation, portfolio assignment, hierarchical guardrails, immutable evaluations, human decisions, fresh-state revalidation, capital reservations, contention, release, expiration, and ResearchOnly behavior.
- Specify the scripted headless demonstration for valid and invalid proposals and concurrent capital demand.
- Add traceability from every Stage 5 criterion to named scenarios and implementing tasks.
- Generate discoverable Reqnroll tests and mark implementation-dependent scenarios with the acceptance harness temporary pending tag; `S5-014` activates them.

## Acceptance Criteria

- Every Stage 5 criterion maps to at least one named business-facing scenario.
- Scenario language identifies exact Bot Run, configuration, Portfolio, snapshot, proposal version, evidence version, policy version, evaluation state, actor, and reservation outcomes where applicable.
- Tags identify Stage 5, proposals, risk, concurrency, recovery, and applicable platforms.
- The Stage 5 filter discovers every scenario with implementation-dependent scenarios explicitly pending.
- Every scenario uses deterministic scripted inputs, injected time and identifiers, migrated temporary SQLite, and simulated application boundaries.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"
.\dev.ps1 format
```

## Completion Notes

Completed 2026-08-20.

- Added six Stage 5 feature files containing 27 named scenarios and 32 discoverable Reqnroll test cases covering proposal recording, evidence and ownership binding, hierarchical risk, immutable evaluations, authorized human decisions, fresh-state revalidation, atomic capital reservations, concurrency, release and recovery, ResearchOnly governance, broker-authority exclusion, and the headless demonstration.
- Added criterion-to-scenario and implementation-task traceability in `tests/Trading.AcceptanceTests/Features/Governance/TRACEABILITY.md`.
- Tagged every Stage 5 specification for cross-platform discovery and applied the temporary `@ignore` acceptance-harness tag until `S5-014` provides production-backed bindings.
- Generated and committed synchronized Reqnroll `.feature.cs` files through the repository build.
- Validation: `.\dev.ps1 build` passed with zero warnings and errors; `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"` discovered 32 tests with 32 explicitly skipped, zero failed, and zero passed; `.\dev.ps1 format` passed; `git diff --check` passed.
- Local validation used the Linux Docker workflow. Windows execution remains delegated to hosted CI at the Stage 5 gate.
- No deviations, follow-up tasks, or ADRs were required. No README, AGENTS.md, or authority update was needed because this task introduced specifications without changing durable project guidance.
