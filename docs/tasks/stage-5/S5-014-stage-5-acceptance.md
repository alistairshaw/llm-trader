---
schema_version: 1
id: S5-014
title: Complete Stage 5 acceptance bindings
stage: 5
status: ready
priority: 700
type: acceptance
depends_on: [S5-013]
labels: [bdd, acceptance, proposals, risk]
created: 2026-08-20
updated: 2026-08-20
---

# S5-014: Complete Stage 5 Acceptance Bindings

## Objective

Bind every Stage 5 scenario to production-backed application workflows and activate the complete cross-platform suite.

## Context

Use [Test Plan — Steps and Drivers](../../test-plan.md#103-steps-and-drivers), [Implementation Plan — Stage 5](../../implementation-plan.md#7-stage-5-trade-proposals-approvals-and-risk), and the traceability matrix created by `S5-001`.

## Scope

- Implement a scenario-scoped Stage 5 application driver using production host composition, migrated temporary SQLite, deterministic substitutes, persistence inspection, and stable diagnostics.
- Route thin Reqnroll steps through explicit business operations for proposals, policies, decisions, reservations, projections, recovery, and the demonstration.
- Remove the Stage 5 temporary pending tags and preserve platform tags.
- Assert application results and durable proposal-governance facts rather than scenario-title-derived outcomes.
- Reconcile the traceability matrix and acceptance documentation with the implemented suite.

## Acceptance Criteria

- The Stage 5 filter passes every scenario twice consecutively with zero failed, pending, or skipped results on the local cross-platform target.
- Every step delegates production behavior to the scenario-scoped driver; feature steps contain no EF, repository, provider, or broker calls.
- The driver observes exact durable proposal, evidence, evaluation, decision, and reservation facts from migrated SQLite.
- Acceptance execution uses scripted and fixture-backed inputs and injected time and identifiers.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending implementation.
