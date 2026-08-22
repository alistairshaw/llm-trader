---
schema_version: 1
id: S6-014
title: Demonstrate the complete paper workflow in the headless host
stage: 6
status: done
priority: 720
type: feature
depends_on: [S6-012, S6-013]
labels: [host, smoke, paper-trading, demonstration]
created: 2026-08-21
updated: 2026-08-22
---

# S6-014: Demonstrate the Complete Paper Workflow in the Headless Host

## Objective

Compose and run the first complete deterministic research-to-fill vertical trading slice.

## Context

Use [Implementation Plan — Stage 6 Demonstration](../../implementation-plan.md#8-stage-6-paper-order-execution), [Architecture — Trading.Host](../../architecture.md#68-tradinghost), and [Local Development — Application Execution](../../local-development.md#2-application-execution).

## Scope

- Register Stage 6 repositories, simulated broker, durable workers, submission, reconciliation, event, Fill, accounting, recovery, and projection services in the Generic Host.
- Extend `dev.ps1 run` to execute deterministic fixture-backed research, Proposal, Approval, Reservation, Order, partial Fill, and final Fill workflow.
- Print bounded stable identities, state transitions, hashes, Position, trade/fee ledger effects, reservation disposition, reconciliation, and complete audit chain.
- Demonstrate duplicate submission/event protection, timeout-after-acceptance reconciliation, and clean restart recovery.
- Preserve canonical SQLite ownership and asynchronous disposal so Windows cleanup succeeds immediately.
- Update README, AGENTS.md, architecture, local-development, Trading Bot, data-model, and test-plan documentation to match the runnable workflow.

## Acceptance Criteria

- `dev.ps1 run` completes with one filled paper Order, exact Position and ledger results, and zero live submissions.
- Repeated runs from rebuilt smoke state reproduce stable business identities and outcomes.
- Demonstrated retries and duplicate events do not duplicate Orders, Fills, or accounting.
- Host startup performs migration and reconciliation before execution workers become ready.
- Graceful shutdown releases every scope, context, connection, provider, host, and exact SQLite pool owner.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=HeadlessHost"
.\dev.ps1 run
.\dev.ps1 run
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

- Registered the Stage 6 paper repositories, conversion, durable processors, simulated broker, reconciliation,
  status-event, Fill-accounting, recovery, and projection ports in `Trading.Host`. The fixed workflow converts the
  governed Proposal, exercises timeout-after-acceptance reconciliation, ingests acknowledgement plus partial/final
  execution events, filters an exact duplicate source message, and verifies the fully correlated projection.
- The stable outcome is Order `01J5QH8M000000000000000001`, client identity
  `paper-0189b4bdb753e1f6fabf521e1fc83ba9ff9686e86d78ba38`, broker Order `paper-broker-0004`, two Fills,
  70 shares, 700 USD gross execution, 2 USD fees, a consumed 700 USD Reservation, 18 projected audit events, and
  zero live submissions. Two clean `./dev.ps1 run` executions reproduced those business facts.
- Validation: `./dev.ps1 build` passed with zero warnings/errors; `Category=HeadlessHost` passed 3/3;
  `Category=Stage6Migrations` passed 9/9; the full suite passed 1,109 with 34 expected pending Stage 6 acceptance
  cases; `./dev.ps1 format` passed; EF reported no pending model changes; and the smoke passed twice.
- Updated README, AGENTS, architecture, data model, Trading Bot, local-development, and test-plan guidance. The
  Stage 5 acceptance driver explicitly disables only the Stage 6 extension so its historical governance assertions
  continue to observe the Stage 5 boundary. Deviations: none. Follow-up tasks: none. ADRs: none.
