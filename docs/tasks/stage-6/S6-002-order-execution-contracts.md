---
schema_version: 1
id: S6-002
title: Define order execution and broker contracts
stage: 6
status: ready
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

Pending.
