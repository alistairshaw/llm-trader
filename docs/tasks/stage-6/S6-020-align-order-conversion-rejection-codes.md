---
schema_version: 1
id: S6-020
title: Align Proposal-to-Order rejection codes with the execution contract
stage: 6
status: ready
priority: 980
type: defect
depends_on: [S6-014]
labels: [execution, contracts, compatibility, acceptance]
created: 2026-08-22
updated: 2026-08-22
---

# S6-020: Align Proposal-to-Order Rejection Codes with the Execution Contract

## Objective

Return the committed Stage 6 rejection codes from Proposal-to-Order conversion.

## Context

The executable contract in `ProposalOrderConversion.feature` requires the stable `order_execution` reason namespace.
Production conversion currently exposes three different `order_conversion` values for the same outcomes, preventing
the production-backed Stage 6 acceptance bindings in `S6-015` from asserting the committed behavior.

Use [Architecture — Paper Execution](../../architecture.md), [Data Model — Order Execution](../../data-model.md), and
[Test Plan — Stage 6](../../test-plan.md).

## Scope

- Make missing approval return `order_execution.approval_required`.
- Make proposal expiry return `order_execution.proposal_expired`.
- Make changed proposal content or stale validated state return `order_execution.fresh_validation_required`.
- Update the centralized conversion constants and every production mapping that exposes these outcomes.
- Update affected Engine, Data, integration, acceptance-contract, and documentation assertions.
- Preserve existing persisted audit facts through explicit compatibility handling only when inspection proves committed
  data can contain one of the replaced values.

## Acceptance Criteria

- Each of the three rejection paths returns its exact committed `order_execution` reason.
- Conversion remains side-effect free for every rejection: no Order or submission outbox is created and no Reservation
  is consumed.
- Exact successful conversion and retry behavior remain unchanged.
- Repository documentation names the production codes returned by the application.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "TestCategory=OrderConversion"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "TestCategory=OrderConversion"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "TestCategory=OrderConversion"
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending.
