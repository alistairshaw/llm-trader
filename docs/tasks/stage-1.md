# Stage 1 Backlog: Solution Foundation and Domain Model

Stage goal and exit criteria: [Implementation Plan — Stage 1](../implementation-plan.md#3-stage-1-solution-foundation-and-domain-model).

Task workflow and priority rules: [Task Management](../task-management.md).

## Current Next Task

`S1-014` — Add Windows and Linux CI (awaiting hosted CI validation).

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
| [`S1-014`](stage-1/S1-014-ci-pipeline.md) | Add Windows and Linux CI | Review | 700 | `S1-005` |
| [`S1-015`](stage-1/S1-015-stage-1-acceptance-review.md) | Complete BDD bindings and Stage 1 acceptance review | Planned | 1000 | `S1-009`–`S1-014` |

## Stage Exit Gate

- All Stage 1 task dependencies resolve and all stage-blocking tasks are `done`.
- All Stage 1 Reqnroll scenarios execute and pass on Windows and Linux.
- All unit and architecture tests pass.
- WPF builds on Windows; non-WPF projects build on Windows and Linux.
- The Stage Review Record is complete.

## Completion Summary

Not started.
