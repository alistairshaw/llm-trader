# Stage 5 Backlog: Trade Proposals, Approvals, and Risk

Stage goal and exit criteria: [Implementation Plan — Stage 5](../implementation-plan.md#7-stage-5-trade-proposals-approvals-and-risk).

Task workflow and priority rules: [Task Management](../task-management.md).

## Current Next Task

No Stage 5 task remains. Stage 5 is complete, and Stage 6 planning may begin.

## Ordered Backlog

| ID | Task | Status | Priority | Depends on |
| --- | --- | --- | ---: | --- |
| [`S5-001`](stage-5/S5-001-write-stage-5-gherkin.md) | Write Stage 5 executable Gherkin specifications | Done | 1000 | — |
| [`S5-002`](stage-5/S5-002-proposal-domain-contracts.md) | Define proposal governance domain and contracts | Done | 940 | `S5-001` |
| [`S5-003`](stage-5/S5-003-proposal-persistence.md) | Add the Stage 5 proposal persistence migration | Done | 920 | `S5-002` |
| [`S5-004`](stage-5/S5-004-proposal-repositories.md) | Implement proposal governance repositories | Done | 900 | `S5-003` |
| [`S5-005`](stage-5/S5-005-proposal-tool-dispatch.md) | Implement structured proposal tool dispatch | Done | 880 | `S5-002`, `S5-004` |
| [`S5-006`](stage-5/S5-006-hierarchical-guardrails.md) | Implement hierarchical guardrail policies | Done | 860 | `S5-002` |
| [`S5-007`](stage-5/S5-007-guardrail-evaluations.md) | Persist immutable guardrail evaluations | Done | 840 | `S5-004`, `S5-006` |
| [`S5-008`](stage-5/S5-008-human-approvals.md) | Implement authorized human proposal decisions | Done | 820 | `S5-004`, `S5-007` |
| [`S5-009`](stage-5/S5-009-capital-reservations.md) | Implement atomic capital reservations | Done | 800 | `S5-004`, `S5-008` |
| [`S5-010`](stage-5/S5-010-research-only-governance.md) | Enforce ResearchOnly proposal governance | Done | 780 | `S5-005`, `S5-007` |
| [`S5-011`](stage-5/S5-011-proposal-orchestration.md) | Orchestrate proposal validation and approval | Done | 760 | `S5-005`, `S5-007`, `S5-008`, `S5-009`, `S5-010` |
| [`S5-012`](stage-5/S5-012-proposal-projections.md) | Build proposal queue and risk projections | Done | 740 | `S5-004`, `S5-011` |
| [`S5-013`](stage-5/S5-013-headless-stage-5-demo.md) | Demonstrate proposal governance in the headless host | Done | 720 | `S5-011`, `S5-012` |
| [`S5-014`](stage-5/S5-014-stage-5-acceptance.md) | Complete Stage 5 acceptance bindings | Done | 700 | `S5-013` |
| [`S5-016`](stage-5/S5-016-windows-sqlite-fixture-disposal.md) | Make SQLite fixture and host disposal Windows-safe | Done | 1100 | `S5-014` |
| [`S5-017`](stage-5/S5-017-release-headless-smoke-database.md) | Release the headless smoke database on host disposal | Done | 1100 | `S5-016` |
| [`S5-018`](stage-5/S5-018-own-all-headless-sqlite-pools.md) | Own every headless smoke SQLite pool and context | Done | 1100 | `S5-017` |
| [`S5-015`](stage-5/S5-015-stage-5-review.md) | Complete Stage 5 acceptance and review | Done | 1000 | `S5-001`–`S5-014`, `S5-016`–`S5-018` |

## Stage Exit Gate

- Every Stage 5 task is `done`.
- Every Stage 5 Reqnroll scenario passes on Windows and Linux with zero pending or skipped scenarios.
- The Stage 5 migration succeeds against a new database and the completed Stage 4 fixture.
- Deterministic tests prove structured proposal creation, exact evidence binding, hierarchical policy, immutable evaluations, authorized decisions, fresh-state revalidation, atomic reservation, concurrency isolation, expiration release, and ResearchOnly behavior.
- Architecture tests prove Trading Bot tools and Stage 5 workflows cannot reach broker submission APIs.
- The headless host demonstrates valid and invalid scripted proposals, structured policy results, approval, reservation contention, and recoverable shutdown.
- Release build, formatting, architecture, unit, data, Engine, integration, acceptance, migration, and security gates pass.
- Hosted Windows and Linux CI passes on the completed Stage 5 revision.
- The Stage 5 Review Record approves beginning Stage 6.

## Completion Summary

All 18 Stage 5 tasks are done. `S5-018` identified the final Windows lock as a distinct inspection connection-string pool and established one canonical absolute `smoke.db` identity for production composition, inspection, diagnostics, and exact cleanup. `S5-015` repeated the complete local gate with 1000 passing tests and zero skipped. Exact revision `61296ca1536a702e48324ef5caeb475603a893e7` then passed hosted Windows, Linux, and security validation.

Stage 5 delivers structured immutable proposals, exact evidence binding, monotonic hierarchical policies, immutable revalidation, authorized decisions, atomic capital reservations, ResearchOnly non-executability, bounded projections, production-backed acceptance, deterministic smoke demonstration, and Windows-safe SQLite ownership. All earlier failed hosted candidates are superseded. No unresolved critical or high-severity defect, exception, ADR, or deferred Stage 5 follow-up remains. Stage 6 commencement is approved.
