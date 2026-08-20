---
schema_version: 1
id: S5-004
title: Implement proposal governance repositories
stage: 5
status: done
priority: 900
type: feature
depends_on: [S5-003]
labels: [repositories, sqlite, proposals, transactions]
created: 2026-08-20
updated: 2026-08-20
---

# S5-004: Implement Proposal Governance Repositories

## Objective

Implement aggregate repositories and atomic persistence operations for Stage 5 governance workflows.

## Context

Use [Architecture — Persistence](../../architecture.md#62-tradingdata), [Domain Model — Repository Boundaries](../../domain.md#10-repository-and-transaction-boundaries), [Data Model — Transaction Boundaries](../../data-model.md#15-transaction-boundaries), and [Test Plan — Data Integration Tests](../../test-plan.md#6-data-integration-tests).

## Scope

- Implement repositories for `Hypothesis`, `TradeProposal`, and `CapitalReservation` aggregate roots with domain-only mappings.
- Implement intent-oriented operations for Hypothesis version creation/freezing and exact-version lookup, idempotent proposal recording, lifecycle concurrency, evaluation append, immutable decision append, active-reservation lookup, expiration, and release.
- Implement transaction services for atomic proposal decision and reservation changes.
- Reconstruct exact proposal, evidence, evaluation, approval, and reservation history in deterministic order.

## Acceptance Criteria

- Real-SQLite tests prove aggregate round trips, idempotency, optimistic concurrency, rollback, append-only history, and deterministic reconstruction.
- Repository APIs expose domain aggregates and intent-oriented results without EF entities, `DbSet`, or `IQueryable`.
- Concurrent repository operations preserve one active reservation per proposal and immutable decision history.
- All cross-aggregate atomic operations use the explicit unit-of-work boundary.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ProposalRepositories"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented domain-only repositories for Hypothesis, TradeProposal, and CapitalReservation with exact-version
rehydration, deterministic evidence/audit ordering, explicit idempotency and optimistic-concurrency outcomes,
append-only evaluation and decision persistence, active-reservation queries and expiration, and an atomic
proposal-decision/reservation transaction boundary. Corrected the unreleased Stage 5 migration's Hypothesis and
TradeProposal status constraints to use the authoritative domain lifecycle names.

Validation completed on 2026-08-20:

- `.\dev.ps1 build` — passed with zero warnings and errors.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ProposalRepositories"` — 5 passed.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests` — 140 passed.
- `.\dev.ps1 test -Project tests/Trading.Core.Tests` — 473 passed.
- `.\dev.ps1 test -Project tests/Trading.Engine.Tests` — 61 passed.
- `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` — 17 passed.
- `.\dev.ps1 test` — 903 passed; 32 Stage 5 acceptance scenarios remain intentionally pending S5-014.
- `.\dev.ps1 format` — passed.
- EF model drift — passed through `StageFiveSchemaHasRestrictionsIndexesAndNoModelDrift` in the Data suite.

No deviations, follow-up tasks, or ADRs.
