---
schema_version: 1
id: S5-009
title: Implement atomic capital reservations
stage: 5
status: planned
priority: 800
type: feature
depends_on: [S5-004, S5-008]
labels: [capital, concurrency, reservations, sqlite]
created: 2026-08-20
updated: 2026-08-20
---

# S5-009: Implement Atomic Capital Reservations

## Objective

Reserve approved proposal capital atomically against current Portfolio availability and active reservations.

## Context

Use [Domain Model — CapitalReservation Aggregate](../../domain.md#82-capitalreservation-aggregate), [Data Model — Capital Reservations](../../data-model.md#95-capital_reservations), [Trading Bot — Isolation and Concurrency](../../trading-bot.md#12-isolation-and-concurrency), and [Test Plan — Capital and Concurrency](../../test-plan.md#capital-and-concurrency).

## Scope

- Calculate required exact currency capital from the approved proposal and fresh state.
- Revalidate approval binding and available capital inside the reservation transaction.
- Create one active reservation idempotently and include all active same-Portfolio reservations in availability.
- Implement deterministic release on rejection and cancellation and expiration through injected time.
- Enforce Portfolio, Bot, currency, and proposal isolation under concurrent writers.

## Acceptance Criteria

- Approval state transition and reservation creation commit atomically for the exact proposal version and fresh state.
- Concurrent proposals cannot reserve more than available same-currency Portfolio capital.
- Reservations in other Portfolios cannot affect availability or become accessible across identity boundaries.
- Release and expiration are idempotent, timestamped, and restore available capital exactly once.
- Real-SQLite contention tests produce one safe winner and stable loser results without corruption.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=CapitalReservation"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=CapitalReservation"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=CapitalConcurrency"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending implementation.
