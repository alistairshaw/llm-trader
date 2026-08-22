---
schema_version: 1
id: S6-007
title: Convert approved proposals to order intents atomically
stage: 6
status: ready
priority: 860
type: feature
depends_on: [S6-005]
labels: [proposals, orders, reservations, transaction]
created: 2026-08-21
updated: 2026-08-21
---

# S6-007: Convert Approved Proposals to Order Intents Atomically

## Objective

Create one paper Order intent and submission work item from an executable Proposal through deterministic fresh-state authorization.

## Context

Use [Trading Bot — Proposal Validation and Execution](../../trading-bot.md#10-proposal-validation-and-execution), [Domain Model — Trade Proposal](../../domain.md#81-tradeproposal-aggregate), [Domain Model — Capital Reservation](../../domain.md#82-capitalreservation-aggregate), and [Data Model — Unit of Work and Transactions](../../data-model.md#13-unit-of-work-and-transactions).

## Scope

- Revalidate the exact approved Proposal, Approval, fresh decision snapshot, policy evaluation, Reservation, Portfolio assignment, broker account, instrument mapping, and paper execution mode immediately before conversion.
- Derive immutable normalized Order terms and a globally unique stable client order ID from durable business identity.
- Commit Proposal conversion, Order intent, Reservation binding, and submission outbox message in one transaction.
- Make exact retries return the existing Order and reject changed, expired, cancelled, ResearchOnly, unreconciled, restricted, or environment-mismatched state with stable codes.
- Persist reconstructable authorization and conversion audit references.

## Acceptance Criteria

- An Order originates only from an approved, unexpired, freshly validated Proposal with an active exact Reservation.
- Order intent and submission work always appear together or not at all.
- Concurrent and repeated conversion produces one Order and one logical submission command.
- Every Order remains bound to exact proposal, approval, evaluation, snapshot, account, instrument mapping, and environment identities.
- Failed authorization changes no Proposal, Reservation, Order, or outbox state.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ProposalOrderConversion"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=OrderConversionTransaction"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending.
