---
schema_version: 1
id: S6-013
title: Build order, fill, and execution audit projections
stage: 6
status: ready
priority: 740
type: feature
depends_on: [S6-011]
labels: [projections, orders, fills, audit]
created: 2026-08-21
updated: 2026-08-22
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

Pending.
