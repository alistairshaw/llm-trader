---
schema_version: 1
id: S5-009
title: Implement atomic capital reservations
stage: 5
status: done
priority: 800
type: feature
depends_on: [S5-004, S5-008]
labels: [capital, concurrency, reservations, sqlite]
created: 2026-08-20
updated: 2026-08-20
owner: s5_009
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

Implemented deterministic exact-currency reservation calculation for priced direct buys and target allocations,
plus a provider-neutral reservation service with stable idempotency, rejection, contention, release, and expiration
outcomes. Added a serializable SQLite reservation boundary that rechecks the immutable approval/content binding,
Portfolio and Trading Bot ownership, fresh snapshot identity/hash/time, currency, proposal lifetime, and all unexpired
same-Portfolio reservations before inserting one active claim. Exact retries reuse the durable claim; rejection,
cancellation, and injected-time expiration release capacity idempotently without exposing order or broker authority.

Validation completed on 2026-08-20: `./dev.ps1 build` passed with zero warnings and errors; focused Core reservation
tests passed 102, Engine reservation tests passed 4, Data reservation tests passed 2, and real-SQLite integration
contention passed 1; the Stage 5 migration/model-drift test passed 1; `./dev.ps1 test` passed 945 tests with 32
intentionally pending Stage 5 acceptance scenarios and no failures; and `./dev.ps1 format` passed. The first
format invocation could not read Docker Desktop configuration in the sandbox; the approved Docker invocation passed.

Updated the README, AGENTS.md, Data Model, and Trading Bot documentation with the durable reservation transaction
and authority rules. No scope deviations, follow-up tasks, or ADRs were created. Hosted Windows validation remains
delegated to the Stage 5 review task.
