---
schema_version: 1
id: S6-010
title: Process broker acknowledgements and order outcomes
stage: 6
status: planned
priority: 800
type: feature
depends_on: [S6-006, S6-008]
labels: [broker-events, acknowledgement, rejection, cancellation]
created: 2026-08-21
updated: 2026-08-21
---

# S6-010: Process Broker Acknowledgements and Order Outcomes

## Objective

Apply normalized broker acknowledgements, rejections, cancellations, and expirations idempotently to Orders.

## Context

Use [Domain Model — Order](../../domain.md#91-order-aggregate), [Data Model — Infrastructure Tables](../../data-model.md#11-infrastructure-tables), and [Test Plan — Orders and Fills](../../test-plan.md#102-initial-journey-catalog).

## Scope

- Dispatch claimed inbox messages to exact account and Order identities with source-event deduplication.
- Apply acknowledged, rejected, cancel-requested, cancelled, expired, and reconciliation-required outcomes through the Order state machine.
- Atomically record the event outcome, Order transition, inbox completion, Reservation release request, and follow-up work.
- Reject, defer, or reconcile unknown Orders, identity mismatches, impossible transitions, stale events, and events that conflict with fill state using stable codes.
- Preserve canonical bounded raw-message hashes and normalized audit facts.

## Acceptance Criteria

- Duplicate messages change Order and Reservation workflow once.
- Valid events produce only state-machine-permitted transitions.
- Terminal Orders never return to active states.
- Out-of-order and conflicting events produce durable safe dispositions without losing the message.
- Rejection, cancellation, and expiration release only the remaining reserved capital.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=OrderExecution"
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=BrokerOrderEvents"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=BrokerInboxOutbox"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending.
