---
schema_version: 1
id: S6-015
title: Complete production-backed Stage 6 acceptance bindings
stage: 6
status: blocked
priority: 700
type: acceptance
depends_on: [S6-014, S6-020]
labels: [bdd, acceptance, production-composition, cross-platform]
created: 2026-08-21
updated: 2026-08-22
owner: s6_015
blocked_reason: Production Proposal-to-Order rejection codes conflict with the committed Stage 6 executable contract; S6-020 must align them first.
---

# S6-015: Complete Production-Backed Stage 6 Acceptance Bindings

## Objective

Activate every Stage 6 scenario through application-facing actions and observable durable outcomes.

## Context

Use [Test Plan — Steps and Drivers](../../test-plan.md#103-steps-and-drivers), [Implementation Plan — Stage 6](../../implementation-plan.md#8-stage-6-paper-order-execution), and [Local Development — Test Strategy](../../local-development.md#4-test-strategy-locally).

## Scope

- Implement a scenario-scoped Stage 6 driver that owns production Generic Host composition, migrated temporary SQLite, deterministic clock and identities, scripted model/research inputs, simulated broker, and persistence inspection.
- Bind all Stage 6 vocabulary through thin Reqnroll steps that call application services and authorized projections.
- Cover conversion, atomic outbox, retry idempotency, all broker outcomes, unknown reconciliation, duplicate and invalid events, partial/final accounting, restart recovery, environment isolation, and the complete demonstration.
- Remove every temporary pending marker from Stage 6 scenarios and keep generated feature code synchronized.
- Emit stable bounded failure diagnostics and asynchronously dispose every SQLite owner before immediate directory deletion.

## Acceptance Criteria

- Every Stage 6 scenario passes with zero failed, pending, ignored, or skipped cases.
- Feature steps contain no EF, repository, broker adapter, or provider calls.
- Assertions observe production application results, authorized projections, and durable records.
- The suite passes twice consecutively in Linux Docker and is eligible for native Windows CI.
- Traceability maps every criterion, scenario, production path, and focused test family.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage6"
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage6"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Blocked on 2026-08-22 after activating the production-backed bindings exposed a stable-contract mismatch. The committed
scenarios require `order_execution.approval_required`, `order_execution.proposal_expired`, and
`order_execution.fresh_validation_required`; production currently returns `order_conversion.proposal_not_approved`,
`order_conversion.proposal_expired`, and `order_conversion.evaluation_mismatch`. The acceptance implementation was
returned rather than weakening or translating the assertions. `S6-020` records the required production correction.
