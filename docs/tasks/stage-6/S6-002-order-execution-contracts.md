---
schema_version: 1
id: S6-002
title: Define order execution and broker contracts
stage: 6
status: done
priority: 960
type: feature
depends_on: [S6-001]
labels: [domain, orders, broker, contracts]
created: 2026-08-21
updated: 2026-08-21
---

# S6-002: Define Order Execution and Broker Contracts

## Objective

Define provider-neutral Order, Fill, broker-operation, reconciliation, and execution result contracts.

## Context

Use [Domain Model — Broker Integration](../../domain.md#6-broker-integration), [Domain Model — Order](../../domain.md#91-order-aggregate), [Architecture — Trading.Engine](../../architecture.md#64-tradingengine), and [Trading Bot — Proposal Validation and Execution](../../trading-bot.md#10-proposal-validation-and-execution).

## Scope

- Complete Order and Fill models with exhaustive transitions, cumulative quantity and fee invariants, globally unique client identity, immutable execution identity, and stable result codes.
- Define normalized paper broker commands and results for submission, client-ID lookup, status reconciliation, cancellation, acknowledgements, rejections, expiration, and executions.
- Define capabilities and explicit paper/live environment identities on every broker operation.
- Define application ports for order conversion, broker access, inbox/outbox work, reconciliation, accounting, clocks, identifiers, transactions, and cancellation.
- Update architecture, domain, Trading Bot, and AGENTS.md guidance when the contracts establish durable execution rules.

## Acceptance Criteria

- Table-driven tests cover every permitted and forbidden Order transition.
- Tests enforce filled quantity at or below ordered quantity and exact cumulative accounting.
- All commands and results are provider-neutral, bounded, canonical, and environment-bound.
- Architecture tests keep broker SDK types outside Core and prohibit LLM access to execution ports.
- Stable codes distinguish accepted, rejected, unknown, retryable, terminal, duplicate, and reconciliation outcomes.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=OrderExecution"
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=BrokerContracts"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

- Added provider-neutral paper broker operation, submission, reconciliation, cancellation, execution, inbox, outbox,
  correlation, idempotency, capability, clock, identifier, transaction, conversion, and accounting contracts.
- Extended Order accounting with exact cumulative gross and fee totals while retaining exhaustive lifecycle authority,
  immutable client/broker execution identities, duplicate execution idempotency, and overfill prevention.
- Made paper and live operation identities structurally distinct and kept all LLM tool dispatch surfaces free of broker
  or order-execution authority.
- Updated `AGENTS.md`, architecture, domain, and Trading Bot documentation with the durable execution rules.
- Validation: `./dev.ps1 build` passed with 0 warnings/errors; focused Core OrderExecution tests passed 9/9;
  focused Engine BrokerContracts tests passed 3/3; architecture tests passed 22/22; the full suite passed 1,015 tests
  with 34 expected temporarily pending Stage 6 acceptance cases and 0 failures; `./dev.ps1 format` passed.
- Deviations: none. Follow-up tasks: none. ADRs: none.
