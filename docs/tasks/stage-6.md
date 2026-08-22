# Stage 6 Backlog: Paper-Order Execution

Stage goal and exit criteria: [Implementation Plan — Stage 6](../implementation-plan.md#8-stage-6-paper-order-execution).

Task workflow and priority rules: [Task Management](../task-management.md).

## Current Next Task

No Stage 6 tasks remain. Stage 7 commencement is approved.

## Ordered Backlog

| ID | Task | Status | Priority | Depends on |
| --- | --- | --- | ---: | --- |
| [`S6-001`](stage-6/S6-001-write-stage-6-gherkin.md) | Write Stage 6 executable Gherkin specifications | Done | 1000 | — |
| [`S6-002`](stage-6/S6-002-order-execution-contracts.md) | Define order execution and broker contracts | Done | 960 | `S6-001` |
| [`S6-003`](stage-6/S6-003-simulated-broker.md) | Implement the deterministic simulated paper broker | Done | 940 | `S6-002` |
| [`S6-004`](stage-6/S6-004-order-persistence.md) | Add the Stage 6 order execution persistence migration | Done | 920 | `S6-002` |
| [`S6-017`](stage-6/S6-017-align-order-persistence-contract.md) | Align order persistence with the execution contract | Done | 950 | `S6-004` |
| [`S6-018`](stage-6/S6-018-align-initial-order-version.md) | Align the initial Order concurrency version | Done | 945 | `S6-017` |
| [`S6-019`](stage-6/S6-019-align-durable-work-persistence.md) | Align durable broker-work persistence | Done | 940 | `S6-018` |
| [`S6-005`](stage-6/S6-005-order-repositories.md) | Implement order execution repositories | Done | 900 | `S6-004`, `S6-017`, `S6-018`, `S6-019` |
| [`S6-006`](stage-6/S6-006-durable-inbox-outbox.md) | Implement durable broker inbox and outbox processing | Done | 880 | `S6-003`, `S6-005` |
| [`S6-007`](stage-6/S6-007-proposal-order-conversion.md) | Convert approved proposals to order intents atomically | Done | 860 | `S6-005` |
| [`S6-008`](stage-6/S6-008-idempotent-order-submission.md) | Submit paper orders with stable client identities | Done | 840 | `S6-006`, `S6-007` |
| [`S6-009`](stage-6/S6-009-submission-reconciliation.md) | Reconcile unknown order submission outcomes | Done | 820 | `S6-008` |
| [`S6-010`](stage-6/S6-010-broker-order-events.md) | Process broker acknowledgements and order outcomes | Done | 800 | `S6-006`, `S6-008` |
| [`S6-011`](stage-6/S6-011-atomic-fill-accounting.md) | Apply partial and final fills atomically | Done | 780 | `S6-009`, `S6-010` |
| [`S6-012`](stage-6/S6-012-execution-recovery.md) | Recover durable paper execution after restart | Done | 760 | `S6-011` |
| [`S6-013`](stage-6/S6-013-order-projections.md) | Build order, fill, and execution audit projections | Done | 740 | `S6-011` |
| [`S6-014`](stage-6/S6-014-headless-paper-demo.md) | Demonstrate the complete paper workflow in the headless host | Done | 720 | `S6-012`, `S6-013` |
| [`S6-020`](stage-6/S6-020-align-order-conversion-rejection-codes.md) | Align Proposal-to-Order rejection codes with the execution contract | Done | 980 | `S6-014` |
| [`S6-015`](stage-6/S6-015-stage-6-acceptance.md) | Complete production-backed Stage 6 acceptance bindings | Done | 700 | `S6-014`, `S6-020` |
| [`S6-016`](stage-6/S6-016-stage-6-review.md) | Complete Stage 6 acceptance and review | Done | 1000 | All Stage 6 implementation tasks |

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

Stage 6 is complete. All 34 production-backed Stage 6 examples pass with zero pending or skipped cases locally and in hosted Windows/Linux CI. Order and durable broker-work persistence exactly represent the Core contracts, durable work is processed with bounded leases and retries, approved paper Proposals convert atomically, unknown outcomes reconcile by client identity, and partial/final executions atomically update Fill audit, Orders, Positions, ledger facts, Reservations, and inbox completion. The deterministic headless host demonstrates the complete governed research-to-final-Fill workflow with stable outcomes and zero live authority. Exact revision and hosted security evidence are recorded in the Stage 6 Review Record. Stage 7 commencement is approved.
