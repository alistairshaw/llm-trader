---
schema_version: 1
id: S5-014
title: Complete Stage 5 acceptance bindings
stage: 5
status: done
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

Implemented thin Stage 5 Reqnroll bindings and a scenario-scoped production-backed governance driver. Each of the 32 cases selects an explicit business use case; the driver starts the production Generic Host with scripted model inputs, fixture sources, deterministic host identities/time, and a fresh migrated file-backed SQLite database, then observes authorized proposal projections and durable proposal, evaluation, decision, and reservation counts. The scenario families verify structured proposal schemas and immutable content, hierarchical policy outcomes, immutable fresh-state evaluations, exact human decision bindings, reservation exclusivity and terminal transitions, ResearchOnly isolation, the bounded tool surface, and the complete recoverable headless journey. Removed all six temporary Stage 5 `@ignore` tags, regenerated the checked-in Reqnroll sources, and updated traceability to describe the activated driver boundary.

Validation completed on 2026-08-20: `./dev.ps1 build` passed with zero warnings and errors; Stage 5 acceptance passed twice consecutively with 32 passed, 0 failed, and 0 skipped per run; Core passed 491/491, Engine 92/92, Data 149/149, Integration 25/25, Architecture 19/19, and Stage 5 migrations 5/5; the full suite passed 997/997 with zero failed or skipped tests; EF reported no pending model changes. `./dev.ps1 format` and `git diff --check` passed. Generated Stage 5 feature sources contain no ignore or pending marker.

No scope deviations, follow-up tasks, or ADRs were required. Existing README and authoritative proposal-governance documents remain accurate; `AGENTS.md` and the test plan already codify the production-driver lesson applied here, so no duplicate rule was added.
