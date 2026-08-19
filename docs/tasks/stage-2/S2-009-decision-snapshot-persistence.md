---
schema_version: 1
id: S2-009
title: Persist immutable Portfolio Decision Snapshots
stage: 2
status: done
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

Implemented the schema-versioned canonical Portfolio Decision Snapshot document, deterministic collection ordering and financial rendering, lowercase SHA-256 hashing, exact whole-artifact reconstruction, relational ownership validation, and an append-only repository. Database triggers reject updates and deletes of published snapshots.

Validation completed on 2026-08-19:

- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=DecisionSnapshots"` — 5 passed.
- `.\dev.ps1 build` — succeeded with 0 warnings and 0 errors.
- `.\dev.ps1 test` — 410 passed across projects: 275 Core, 76 Data, 11 Architecture, and 48 Acceptance; 20 Stage 2 acceptance scenarios intentionally remain skipped pending their implementation tasks.
- `.\dev.ps1 format` — passed.
- EF Core `HasPendingModelChanges()` — false against real SQLite.

No deviations, follow-up tasks, or ADRs.
