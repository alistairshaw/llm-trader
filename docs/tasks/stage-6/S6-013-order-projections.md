---
schema_version: 1
id: S6-013
title: Build order, fill, and execution audit projections
stage: 6
status: done
priority: 740
type: feature
depends_on: [S6-011]
labels: [projections, orders, fills, audit]
created: 2026-08-21
updated: 2026-08-22
owner: s6_013
---

# S6-013: Build Order, Fill, and Execution Audit Projections

## Objective

Expose bounded authorized views of paper Orders, Fills, reconciliation, accounting, and end-to-end audit history.

## Context

Use [Data Model — Read Models](../../data-model.md#18-read-models), [Architecture — Persistence Design](../../architecture.md#13-persistence-design), and [Test Plan — Orders and Fills](../../test-plan.md#102-initial-journey-catalog).

## Scope

- Implement no-tracking queries for Order queues/details, Fill history, broker-account reconciliation, Position and ledger effects, remaining reservations, and durable work status.
- Build a chronological audit chain from Bot Run, Report evidence, Proposal, evaluation, Approval, Reservation, Order conversion, submissions, broker messages, reconciliations, Fills, Positions, and ledger entries.
- Enforce actor grants and exact Bot, Portfolio, account, and report visibility on every query.
- Add bounded paging, stable ordering, canonical status/reason codes, and redacted diagnostics.
- Add query-count and cross-owner isolation tests.

## Acceptance Criteria

- Projections show exact quantities, prices, fees, cash effects, reservation consumption, identities, timestamps, and current state.
- The audit chain reconstructs partial and final execution without reading provider payloads.
- Unauthorized actors and mismatched ownership receive no execution facts.
- Paging is deterministic and bounded.
- Queries use no tracking and expose no persistence implementation types.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=OrderProjections"
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ExecutionAudit"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented immutable EF-free execution projection contracts and registered a no-tracking SQLite query service. Order
queues authorize exact Bot, Portfolio, account, proposal, status, paper environment, and UTC time filters before stable
bounded pagination. Detail projections expose exact fill financials and a redacted chronological chain spanning the Bot
Run, report evidence, Proposal governance, Reservation, Order transitions, durable work, submissions, reconciliation,
Fills, Positions, and ledger effects.

Validation completed on 2026-08-22: `./dev.ps1 build` passed with zero warnings and errors;
`./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=OrderProjections"` passed 4/4;
`./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ExecutionAudit"` passed 1/1;
`./dev.ps1 test` passed 1,104 tests with the 34 planned Stage 6 scenarios pending; and `./dev.ps1 format` passed.
The real-SQLite tests cover exact values, isolation, filters, pagination, empty tracking state, audit ordering, and indexed
queue-plan selection. No migrations or ADRs were added and no follow-up tasks were required.
