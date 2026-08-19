# Stage 1 Backlog: Solution Foundation and Domain Model

Stage goal and exit criteria: [Implementation Plan — Stage 1](../implementation-plan.md#3-stage-1-solution-foundation-and-domain-model).

Task workflow and priority rules: [Task Management](../task-management.md).

## Current Next Task

None. Stage 1 is complete; no Stage 2 task index has been defined in the repository.

## Ordered Backlog

| ID | Task | Status | Priority | Depends on |
| --- | --- | --- | ---: | --- |
| [`S1-001`](stage-1/S1-001-write-stage-1-gherkin.md) | Write Stage 1 executable Gherkin specifications | Done | 1000 | — |
| [`S1-002`](stage-1/S1-002-initialize-solution.md) | Initialize the solution and project skeleton | Done | 950 | `S1-001` |
| [`S1-003`](stage-1/S1-003-build-conventions.md) | Configure shared build conventions | Done | 900 | `S1-002` |
| [`S1-004`](stage-1/S1-004-test-infrastructure.md) | Configure NUnit and Reqnroll test infrastructure | Done | 890 | `S1-003` |
| [`S1-005`](stage-1/S1-005-project-boundaries.md) | Establish project references and architecture tests | Done | 880 | `S1-003`, `S1-004` |
| [`S1-006`](stage-1/S1-006-domain-identifiers.md) | Implement strongly typed domain identifiers | Done | 820 | `S1-004`, `S1-005` |
| [`S1-007`](stage-1/S1-007-financial-value-objects.md) | Implement financial value objects | Done | 810 | `S1-006` |
| [`S1-008`](stage-1/S1-008-policy-value-objects.md) | Implement foundational policy value objects | Done | 800 | `S1-006` |
| [`S1-009`](stage-1/S1-009-bot-aggregates.md) | Implement Trading Bot and Bot Run aggregates | Done | 760 | `S1-007`, `S1-008` |
| [`S1-010`](stage-1/S1-010-portfolio-broker-aggregates.md) | Implement Portfolio and Broker aggregates | Done | 750 | `S1-007`, `S1-008` |
| [`S1-011`](stage-1/S1-011-research-aggregates.md) | Implement Research aggregates | Done | 740 | `S1-006`, `S1-008` |
| [`S1-012`](stage-1/S1-012-proposal-reservation-aggregates.md) | Implement Proposal and Capital Reservation aggregates | Done | 730 | `S1-007`, `S1-008` |
| [`S1-013`](stage-1/S1-013-order-aggregate.md) | Implement the Order aggregate and state machine | Done | 720 | `S1-007` |
| [`S1-014`](stage-1/S1-014-ci-pipeline.md) | Add Windows and Linux CI | Done | 700 | `S1-005` |
| [`S1-015`](stage-1/S1-015-stage-1-acceptance-review.md) | Complete BDD bindings and Stage 1 acceptance review | Done | 1000 | `S1-009`–`S1-014` |

## Stage Exit Gate

- All Stage 1 task dependencies resolve and all stage-blocking tasks are `done`.
- All Stage 1 Reqnroll scenarios execute and pass on Windows and Linux.
- All unit and architecture tests pass.
- WPF builds on Windows; non-WPF projects build on Windows and Linux.
- The Stage Review Record is complete.

## Completion Summary

Completed 2026-08-19.

- All fifteen Stage 1 tasks are `done`, with dependency order and acceptance traceability current.
- All 48 Stage 1 Reqnroll cases pass with zero skipped, pending, or undefined steps.
- The complete local suite passes: 275 Core tests, 6 architecture tests, and 48 acceptance tests. Locked restore, Release build with zero warnings, and formatting verification pass through the Docker workflow.
- Exact public revision `facd9652303dffddc4875f719c6b673c7de516a4` passed Windows and Linux validation in [CI run 32264483096](https://github.com/alistairshaw/llm-trader/actions/runs/32264483096), including native WPF build on Windows and retained TRX artifacts. [Security run 32264481275](https://github.com/alistairshaw/llm-trader/actions/runs/32264481275) also passed.
- The [Stage 1 Review Record](../stage-1-review.md) approves beginning Stage 2. No persistence migration applies to Stage 1, and no follow-up task or ADR remains open.
