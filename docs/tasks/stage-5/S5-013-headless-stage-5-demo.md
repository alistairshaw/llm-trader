---
schema_version: 1
id: S5-013
title: Demonstrate proposal governance in the headless host
stage: 5
status: planned
priority: 720
type: feature
depends_on: [S5-011, S5-012]
labels: [host, smoke, proposals, demonstration]
created: 2026-08-20
updated: 2026-08-20
---

# S5-013: Demonstrate Proposal Governance in the Headless Host

## Objective

Extend the deterministic headless smoke to demonstrate Stage 5 proposal governance end to end.

## Context

Use [Implementation Plan — Stage 5 Demonstration](../../implementation-plan.md#7-stage-5-trade-proposals-approvals-and-risk), [Architecture — Trading.Host](../../architecture.md#66-tradinghost), and [Local Development — Repository Support](../../local-development.md#repository-support).

## Scope

- Compose Stage 5 services in the Generic Host using scripted model responses, fixture Research, deterministic market/account state, injected time and identifiers, and migrated SQLite.
- Demonstrate valid direct-trade and target-allocation proposals, an invalid proposal, structured hierarchical outcomes, an authorized approval, fresh revalidation, successful reservation, and a concurrent insufficient-capital result.
- Print bounded stable identities, versions, policy outcomes, reservation totals, and recoverable shutdown state.
- Keep the default smoke configuration simulated and broker-submission-free.
- Update README, architecture, local-development, test-plan, and AGENTS.md guidance when host behavior or commands change.

## Acceptance Criteria

- `.\dev.ps1 run` completes the Stage 3, Stage 4, and Stage 5 deterministic demonstrations from a fresh database.
- Durable SQLite facts match every printed proposal, evaluation, decision, and reservation identity and outcome.
- Repeated smoke execution starts from its dedicated clean smoke database and produces deterministic business hashes and stable outcomes.
- Graceful shutdown completes within the configured deadline with recoverable durable state.
- Host composition contains simulated inputs and no broker submission implementation.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=Stage5Host"
.\dev.ps1 run
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Pending implementation.
