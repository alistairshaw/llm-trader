# Stage 5 Backlog: Trade Proposals, Approvals, and Risk

Stage goal and exit criteria: [Implementation Plan — Stage 5](../implementation-plan.md#7-stage-5-trade-proposals-approvals-and-risk).

Task workflow and priority rules: [Task Management](../task-management.md).

## Current Next Task

`S5-013` is ready: demonstrate proposal governance in the headless host.

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
| [`S5-013`](stage-5/S5-013-headless-stage-5-demo.md) | Demonstrate proposal governance in the headless host | Ready | 720 | `S5-011`, `S5-012` |
| [`S5-014`](stage-5/S5-014-stage-5-acceptance.md) | Complete Stage 5 acceptance bindings | Planned | 700 | `S5-013` |
| [`S5-015`](stage-5/S5-015-stage-5-review.md) | Complete Stage 5 acceptance and review | Planned | 1000 | `S5-001`–`S5-014` |

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

Stage 5 is in progress. `S5-001` defined 32 discoverable proposal-governance acceptance cases, all temporarily pending until `S5-014`; `S5-002` established the proposal-governance domain and application authority boundary; `S5-003` added the Stage 5 SQLite governance schema and immutable audit safeguards; `S5-004` implemented deterministic domain repositories and atomic governance persistence; `S5-005` added authorized, versioned structured-proposal tools with durable audit and no execution authority; `S5-006` implemented pure deterministic hierarchical guardrails with monotonic child policies and complete structured rule results; `S5-007` added canonically hashed, reconstructable, immutable evaluation artifacts with atomic lifecycle disposition and retry/concurrency semantics; `S5-008` implemented authorization-before-disclosure human decisions bound to exact immutable proposal and reviewed-state artifacts; `S5-009` added exact, idempotent, serializable capital reservation with real-SQLite contention isolation and deterministic release/expiration; `S5-010` pinned execution mode on proposals and made ResearchOnly evaluation durably non-executable across approval, reservation, conversion, and broker boundaries; `S5-011` now orchestrates initial validation, exact human review, post-approval fresh-state revalidation, atomic reservation, retry, bounded failure, and recoverable expiration without order or broker authority; and `S5-012` added bounded, no-tracking proposal queues and exact governance-detail projections with Bot, Portfolio, account, actor, and report-visibility authorization. `S5-013` is ready.
