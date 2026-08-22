---
schema_version: 1
id: S7-011
title: Build execution Fill and risk audit views
stage: 7
status: planned
priority: 840
type: feature
depends_on: [S7-002, S7-005]
labels: [wpf, orders, fills, risk]
created: 2026-08-22
updated: 2026-08-22
---
# S7-011: Build Execution, Fill, and Risk Audit Views

## Objective
Display authorized Orders, Fills, financial effects, risk events, and correlated audit.

## Context
Use [Domain Model](../../domain.md), [Read Models](../../data-model.md#18-read-models), and [Execution Flow](../../architecture.md#9-core-execution-flow).

## Scope
- Build Order queue/detail, transitions, submission/reconciliation, Fills, Position/ledger effects, fees, Reservation, and audit views.
- Add risk filters and prominent rejected, unknown, disconnected, stale, and recovery states.
- Authorize Bot, Portfolio, account, environment, and evidence before paging/disclosure.
- Add accessibility metadata and view-model/query integration tests.

## Out of Scope
None.

## Acceptance Criteria
- Exact quantities, prices, gross, fees, Position effects, and Reservation outcomes match durable state.
- Chronology links run, Research, governance, Order work, reconciliation, Fills, Position, and ledger.
- Duplicate events never duplicate visible financial facts.

## Validation
Build; ExecutionRiskAudit WPF tests; OperatorExecutionAudit Data tests; full tests; format.

## Completion Notes
Pending implementation.
