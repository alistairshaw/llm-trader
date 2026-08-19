---
schema_version: 1
id: S2-009
title: Persist immutable Portfolio Decision Snapshots
stage: 2
status: ready
priority: 790
type: feature
depends_on: [S2-008]
labels: [snapshots, canonical-json, hashing]
created: 2026-08-19
updated: 2026-08-19
---

# S2-009: Persist Immutable Portfolio Decision Snapshots

## Objective

Create reproducible, immutable Portfolio Decision Snapshot artifacts from reconciled portfolio state.

## Context

Follow [Domain Model — PortfolioDecisionSnapshot](../../domain.md#53-portfoliodecisionsnapshot-aggregate), [Data Model — Portfolio Decision Snapshots](../../data-model.md#65-portfolio_decision_snapshots), and the snapshot behavior in [Trading Bot](../../trading-bot.md).

## Scope

- Define the versioned canonical snapshot document containing cash, buying power, reserved capital, positions, open orders, risk utilization, relevant cash flows, reconciliation state, and freshness.
- Implement deterministic ordering and canonical rendering for every collection and financial value.
- Compute the lowercase SHA-256 hash from canonical UTF-8 content.
- Map and implement the Portfolio Decision Snapshot repository as an append-only whole-artifact store.
- Validate ownership links among Portfolio, Trading Bot, configuration version, and snapshot.
- Add deterministic hash fixtures, exact round-trip tests, immutability tests, and equivalent-input ordering tests.

## Acceptance Criteria

- Equivalent state produces byte-identical canonical content and the same hash.
- A material state change produces different content and hash.
- Reloaded snapshots retain exact financial values, identities, timestamps, reconciliation state, freshness, schema version, and hash.
- A stored snapshot cannot be updated or deleted through normal repository operations.
- Snapshot creation rejects inconsistent Portfolio, Trading Bot, or configuration ownership.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=DecisionSnapshots"
.\dev.ps1 build
```

## Completion Notes

Not completed.
