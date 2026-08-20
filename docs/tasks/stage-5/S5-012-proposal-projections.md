---
schema_version: 1
id: S5-012
title: Build proposal queue and risk projections
stage: 5
status: planned
priority: 740
type: feature
depends_on: [S5-004, S5-011]
labels: [projections, proposals, risk, queries]
created: 2026-08-20
updated: 2026-08-20
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

Pending implementation.
