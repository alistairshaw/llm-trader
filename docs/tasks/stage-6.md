# Stage 6 Backlog: Paper-Order Execution

Stage goal and exit criteria: [Implementation Plan — Stage 6](../implementation-plan.md#8-stage-6-paper-order-execution).

Task workflow and priority rules: [Task Management](../task-management.md).

## Current Next Task

[`S6-017`](stage-6/S6-017-align-order-persistence-contract.md) is the next ready task. `S6-005` resumes after that corrective dependency is complete.

## Ordered Backlog

| ID | Task | Status | Priority | Depends on |
| --- | --- | --- | ---: | --- |
| [`S6-001`](stage-6/S6-001-write-stage-6-gherkin.md) | Write Stage 6 executable Gherkin specifications | Done | 1000 | — |
| [`S6-002`](stage-6/S6-002-order-execution-contracts.md) | Define order execution and broker contracts | Done | 960 | `S6-001` |
| [`S6-003`](stage-6/S6-003-simulated-broker.md) | Implement the deterministic simulated paper broker | Done | 940 | `S6-002` |
| [`S6-004`](stage-6/S6-004-order-persistence.md) | Add the Stage 6 order execution persistence migration | Done | 920 | `S6-002` |
| [`S6-017`](stage-6/S6-017-align-order-persistence-contract.md) | Align order persistence with the execution contract | Ready | 950 | `S6-004` |
| [`S6-005`](stage-6/S6-005-order-repositories.md) | Implement order execution repositories | Blocked | 900 | `S6-004`, `S6-017` |
| [`S6-006`](stage-6/S6-006-durable-inbox-outbox.md) | Implement durable broker inbox and outbox processing | Planned | 880 | `S6-003`, `S6-005` |
| [`S6-007`](stage-6/S6-007-proposal-order-conversion.md) | Convert approved proposals to order intents atomically | Planned | 860 | `S6-005` |
| [`S6-008`](stage-6/S6-008-idempotent-order-submission.md) | Submit paper orders with stable client identities | Planned | 840 | `S6-006`, `S6-007` |
| [`S6-009`](stage-6/S6-009-submission-reconciliation.md) | Reconcile unknown order submission outcomes | Planned | 820 | `S6-008` |
| [`S6-010`](stage-6/S6-010-broker-order-events.md) | Process broker acknowledgements and order outcomes | Planned | 800 | `S6-006`, `S6-008` |
| [`S6-011`](stage-6/S6-011-atomic-fill-accounting.md) | Apply partial and final fills atomically | Planned | 780 | `S6-009`, `S6-010` |
| [`S6-012`](stage-6/S6-012-execution-recovery.md) | Recover durable paper execution after restart | Planned | 760 | `S6-011` |
| [`S6-013`](stage-6/S6-013-order-projections.md) | Build order, fill, and execution audit projections | Planned | 740 | `S6-011` |
| [`S6-014`](stage-6/S6-014-headless-paper-demo.md) | Demonstrate the complete paper workflow in the headless host | Planned | 720 | `S6-012`, `S6-013` |
| [`S6-015`](stage-6/S6-015-stage-6-acceptance.md) | Complete production-backed Stage 6 acceptance bindings | Planned | 700 | `S6-014` |
| [`S6-016`](stage-6/S6-016-stage-6-review.md) | Complete Stage 6 acceptance and review | Planned | 1000 | `S6-001`–`S6-015` |

## Stage Exit Gate

- Every Stage 6 task is `done`.
- Every Stage 6 Reqnroll scenario passes on Windows and Linux with zero pending or skipped scenarios.
- Order intent and submission outbox creation are atomic, and stable client order IDs make retries idempotent.
- Duplicate, invalid, and out-of-order broker messages preserve valid Order, Position, ledger, Fill, and Reservation state.
- Unknown submission outcomes reconcile before another submission attempt.
- Partial and final fills atomically update all financial state and consume or release reserved capital exactly once.
- Pending inbox, outbox, submission, reconciliation, and fill work resumes safely after restart.
- Paper and live environments remain structurally distinct and every default execution path uses the deterministic simulated paper broker.
- The headless host demonstrates the complete research-to-final-fill audit chain.
- Release build, formatting, architecture, unit, data, Engine, integration, acceptance, migration, smoke, and security gates pass.
- Hosted Windows and Linux CI passes on the completed Stage 6 revision.
- The Stage 6 Review Record approves beginning Stage 7.

## Completion Summary

Stage 6 implementation is in progress. The executable acceptance contract is complete and 34 temporarily pending Stage 6 test cases are discoverable. Persistence-contract alignment task `S6-017` is ready; repository task `S6-005` is blocked on it.
