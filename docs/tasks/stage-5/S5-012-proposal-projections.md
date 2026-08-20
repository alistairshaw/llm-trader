---
schema_version: 1
id: S5-012
title: Build proposal queue and risk projections
stage: 5
status: done
priority: 740
type: feature
depends_on: [S5-004, S5-011]
labels: [projections, proposals, risk, queries]
created: 2026-08-20
updated: 2026-08-20
owner: s5_012
---

# S5-012: Build Proposal Queue and Risk Projections

## Objective

Expose authorized read-only proposal queues and complete governance detail projections.

## Context

Use [Architecture — Trading.Data](../../architecture.md#62-tradingdata), [Data Model — Read Models and Query Services](../../data-model.md#16-read-models-and-query-services), and [Test Plan — Data Integration Tests](../../test-plan.md#6-data-integration-tests).

## Scope

- Add no-tracking queries for pending human decisions, current proposal status, exact action and evidence, evaluation history, decision history, reservation status, and expiration.
- Authorize every query by actor, Bot, Portfolio, and report visibility before returning facts.
- Provide stable ordering, filtering, pagination, and deterministic empty results.
- Expose canonical structured rule results and exact policy/state versions for application hosts.

## Acceptance Criteria

- Queue queries return only proposals visible to the authorized principal and order ties deterministically.
- Detail queries reconstruct exact immutable evidence, evaluation, approval, and reservation histories.
- Projection tests prove no-tracking behavior, pagination stability, private isolation, and bounded payloads using real SQLite.
- Query interfaces expose projection records rather than EF entities or `IQueryable`.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ProposalProjections"
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ProposalQueries"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented immutable EF-free proposal queue/detail contracts and a no-tracking SQLite query service. Every
non-administrator read intersects explicit Trading Bot, Portfolio, and broker-account grants with persisted
Portfolio ownership, then verifies every referenced report's visibility before returning facts. Queue results
support status, mode, Bot, Portfolio, account, and expiry filters; order deterministically by expiry, creation,
and identity; and paginate after authorization. Detail results reconstruct exact action/content/configuration/
snapshot versions, immutable report and Hypothesis evidence, canonical ordered evaluations and policy/rule
results, decision history, and reservation freshness/expiry state. README and data-model documentation now
describe this boundary.

Validation:

- `./dev.ps1 build` — passed, 0 warnings and 0 errors.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ProposalProjections"` — 4 passed.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ProposalQueries"` — 2 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests` — 149 passed.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests` — 92 passed.
- `./dev.ps1 test -Project tests/Trading.Architecture.Tests` — 19 passed.
- `./dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage5Migrations"` — 5 passed,
  including EF model drift.
- `./dev.ps1 test` — 965 passed, 32 intentionally pending Stage 5 acceptance cases, 0 failed.
- `./dev.ps1 format` — passed.

No deviations, follow-up tasks, or ADRs.
