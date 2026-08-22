---
schema_version: 1
id: S6-003
title: Implement the deterministic simulated paper broker
stage: 6
status: done
priority: 940
type: feature
depends_on: [S6-002]
labels: [broker, simulation, paper, deterministic]
created: 2026-08-21
updated: 2026-08-21
---

# S6-003: Implement the Deterministic Simulated Paper Broker

## Objective

Provide a deterministic paper broker adapter that exercises the complete broker contract without network access.

## Context

Use [Implementation Plan — Stage 6](../../implementation-plan.md#8-stage-6-paper-order-execution), [Domain Model — Broker Connection](../../domain.md#61-brokerconnection-aggregate), [Local Development — Application Execution](../../local-development.md#2-application-execution), and [Test Plan — Test Doubles](../../test-plan.md#53-test-doubles).

## Scope

- Implement configurable scripted acceptance, rejection, timeout-after-acceptance, cancellation, expiration, partial-fill, and final-fill behavior.
- Retain broker Orders by stable client order ID and return the same broker identity for exact duplicate submissions.
- Emit deterministic bounded broker messages with unique source event and execution identities.
- Implement client-ID lookup and account/order reconciliation from simulated broker state.
- Reject every operation whose connection, account, or command environment is not paper.
- Document the simulator configuration and deterministic fixture behavior in local-development guidance.

## Acceptance Criteria

- Common contract tests pass for submission, lookup, reconciliation, cancellation, and event delivery.
- Timeout-after-acceptance proves lookup returns the accepted Order without another broker Order.
- Duplicate submission and duplicate event scripts remain deterministic and idempotent.
- Paper/live mismatch is rejected before adapter state changes.
- Tests use no network, credentials, wall clock, locale, or random identity source.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=SimulatedBroker"
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=BrokerContracts"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

- Added the deterministic, network-free `SimulatedPaperBroker` with an explicit capability profile, exact paper
  connection/account/environment binding, stable client-ID deduplication, client-ID lookup and reconciliation, and
  injected UTC clock, identity, latency, and cancellation seams.
- Added bounded per-order scripts for acceptance, rejection, unknown submission, timeout after acceptance,
  cancellation outcomes, expiration, partial/final fills, duplicate messages, and out-of-order delivery. Duplicate
  events retain their source and execution identities, and exact duplicate submissions retain one broker Order.
- Moved the provider-neutral paper gateway port from Engine to Core so `Trading.Brokers` can implement it without a
  dependency cycle; documented this durable boundary and the simulator fixture contract in architecture and local
  development guidance.
- Validation: `./dev.ps1 build` passed with 0 warnings/errors; focused simulated-broker contract tests passed 15/15;
  focused Engine broker-contract tests passed 3/3; architecture tests passed 22/22; the full suite passed 1,030 tests
  with 34 expected temporarily pending Stage 6 acceptance cases and 0 failures; `./dev.ps1 format` passed.
- The first isolated-worktree build required `./dev.ps1 restore` because that worktree had no assets; locked restore
  passed. Windows and Linux hosted validation remains delegated to the Stage 6 review task.
- Deviations: none. Follow-up tasks: none. ADRs: none.
