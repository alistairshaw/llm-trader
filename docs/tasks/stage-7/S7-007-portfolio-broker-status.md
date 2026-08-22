---
schema_version: 1
id: S7-007
title: Build Portfolio and broker status views
stage: 7
status: done
priority: 860
type: feature
depends_on: [S7-002, S7-005]
labels: [wpf, portfolio, broker]
created: 2026-08-22
updated: 2026-08-22
---
# S7-007: Build Portfolio and Broker Status Views

## Objective
Display authorized Portfolio, Position, capital, account, connection, mapping, and reconciliation state.

## Context
Use [Domain Model](../../domain.md), [Read Models](../../data-model.md#18-read-models), and [Trading.Brokers](../../architecture.md#63-tradingbrokers).

## Scope
- Build Portfolio list/detail, Positions, ledger summary, capital, account association, capabilities, mappings, and reconciliation views.
- Add filters, deterministic paging, and loading, empty, stale, disconnected, uncertain, and denied states.
- Format financial values, timestamps, and environment labels unambiguously.
- Add accessibility metadata and view-model/query integration tests.

## Out of Scope
None.

## Acceptance Criteria
- Views preserve exact financial values and ownership isolation.
- Disconnected/uncertain states are prominent in text and automation state.
- Every broker-bearing view explicitly identifies the paper environment.

## Validation
Build; PortfolioBroker WPF tests; OperatorPortfolioBroker Data tests; full tests; format.

## Completion Notes
Implemented an ownership-scoped, no-tracking Portfolio/broker projection with deterministic search, status filtering,
ordering, and paging. The projection preserves exact capital, Position, and ledger decimals and returns account,
connection, paper environment, capabilities, active mappings, and latest reconciliation state. Added the accessible WPF
Portfolio/broker view and independently testable view model with explicit loading, empty, denied, stale, disconnected,
uncertain, and paper-environment text and automation state.

Validation:

- `./dev.ps1 restore -RefreshLocks` — passed; refreshed the WPF test project's lock file for its Core project reference.
- `./dev.ps1 build` — passed with zero warnings and zero errors.
- `./dev.ps1 test -Project tests/Trading.UI.Wpf.Tests -Filter "TestCategory=PortfolioBroker"` — 4 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "TestCategory=OperatorPortfolioBroker"` — 2 passed.
- `./dev.ps1 test` — 1,177 passed, 4 pre-existing Stage 7 acceptance placeholders skipped, 0 failed.
- `./dev.ps1 format` — passed with no changes required.

No scope deviations, follow-up tasks, or ADRs.
