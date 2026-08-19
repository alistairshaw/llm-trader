---
schema_version: 1
id: S1-010
title: Implement Portfolio and Broker aggregates
stage: 1
status: done
priority: 750
type: feature
depends_on: [S1-007, S1-008]
labels: [domain, portfolio, broker, instrument]
created: 2026-08-19
updated: 2026-08-19
---

# S1-010: Implement Portfolio and Broker Aggregates

## Objective

Implement the Stage 1 domain behavior for Portfolios, Positions, snapshots, ledger facts, Broker Connections, Broker Accounts, and Instruments.

## Scope

- Implement `Portfolio`, `Position`, `PortfolioDecisionSnapshot`, and `PortfolioLedgerEntry`.
- Implement `BrokerConnection`, `BrokerAccount`, `Instrument`, and `InstrumentBrokerMapping`.
- Encode ownership, lifecycle, stable instrument identity, mapping intervals, immutability, and financial-state invariants.

## Out of Scope

- EF Core mappings and repositories.
- Live broker SDKs.
- Complete position accounting and corporate-action policy.
- Snapshot generation service.

## Acceptance Criteria

- A Portfolio permits at most one active Trading Bot assignment.
- Base currency cannot change after financial activity begins.
- Closed Portfolios reject new activity.
- Position changes require a recognized execution or audited adjustment reference.
- Decision Snapshots and ledger entries are immutable.
- Broker environment distinguishes paper from live.
- Instrument identity does not rely on ticker alone.
- Overlapping ambiguous broker mappings are rejected.
- Aggregate invariants have positive and negative tests.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=PortfolioOrBrokerAggregates"
```

## Completion Notes

Implemented the Stage 1 Portfolio and Broker Integration domain model in `Trading.Core`:

- Added Portfolio lifecycle, single-bot and single-account ownership, audited capital changes, base-currency locking, and snapshot authorization.
- Added idempotent Position changes limited to recognized executions or audited adjustments.
- Added immutable Portfolio Decision Snapshots and Portfolio Ledger Entries.
- Added explicit paper/live Broker Connections, normalized Broker Accounts with reconciliation/order safety, and one-active-portfolio assignment.
- Added stable Instruments and effective-time Broker Mappings with ambiguous-overlap rejection.
- Added positive and negative aggregate tests under the `PortfolioOrBrokerAggregates` category.

Validation completed in the Linux Docker development container:

- `.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=PortfolioOrBrokerAggregates"` — passed, 5 tests.
- `.\dev.ps1 build` — passed in Release with 0 warnings and 0 errors.
- `.\dev.ps1 test` — passed: 116 Core tests, 6 Architecture tests, and 1 Acceptance test; 47 intentionally deferred Acceptance scenarios skipped.
- `.\dev.ps1 format` — passed with no formatting changes required after applying the formatter.

Deviations: none. Complete position accounting, corporate-action policy, persistence, broker SDK integration, and snapshot generation remain out of scope as specified.

Follow-up tasks: none created. ADRs: none created or changed.
