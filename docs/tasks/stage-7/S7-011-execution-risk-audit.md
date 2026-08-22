---
schema_version: 1
id: S7-011
title: Build execution Fill and risk audit views
stage: 7
status: done
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

- Added an authorized execution workspace with bounded Order paging, Order/client/correlation search, risk-state
  filtering, exact Fill and financial presentation, Reservation outcome, Position/ledger effects, and a correlated
  chronological audit spanning Research, Bot Run, governance, broker work, reconciliation, Fill, Position, and ledger.
- Extended the durable execution projection with exact Position and ledger effects. The existing execution query
  continues to authorize Trading Bot, Portfolio, broker account, broker environment, and Research evidence before
  paging or detail disclosure. The view model deduplicates every displayed financial and audit fact by durable identity.
- Added stable automation IDs, accessible names, a heading, labelled filters, assertive risk announcements, keyboard
  navigation, and read-only data grids. Rejected, unknown, disconnected, stale, and recovery signals are prominent.
- Validation: `./dev.ps1 restore` passed in locked mode; `./dev.ps1 build` passed with zero warnings and errors;
  `./dev.ps1 test -Project tests/Trading.UI.Wpf.Tests -Filter "TestCategory=ExecutionRiskAudit"` passed 4/4;
  `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "TestCategory=OperatorExecutionAudit"` passed 4/4;
  `./dev.ps1 test` passed 1,203 with 4 expected Stage 7 scenario skips and zero failures; `./dev.ps1 format` passed.
- Windows-native WPF execution remains delegated to CI. No deviations, follow-up tasks, or ADRs.
