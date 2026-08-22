---
schema_version: 1
id: S7-002
title: Define authorized operator application contracts
stage: 7
status: done
priority: 960
type: feature
depends_on: [S7-001]
labels: [engine, authorization, projections]
created: 2026-08-22
updated: 2026-08-22
---
# S7-002: Define Authorized Operator Application Contracts

## Objective
Provide UI-neutral authorized query and command contracts for every operator workflow.

## Context
Use [Trading.Engine](../../architecture.md#64-tradingengine), [Trading.UI.Wpf](../../architecture.md#67-tradinguiwpf), and [Read Models](../../data-model.md#18-read-models).

## Scope
- Define immutable principal, permission, page, filter, summary, detail, warning, command, progress, and result contracts.
- Add services for Bot lifecycle/configuration/assignment, manual runs, Research requests, Proposal decisions, and operational reads.
- Enforce authorization before disclosure or mutation with stable results and cancellation.
- Add unit, integration, and architecture tests.

## Out of Scope
None.

## Acceptance Criteria
- Every Stage 7 read/action is available through typed asynchronous contracts.
- Unauthorized resources produce stable non-disclosing results.
- Contracts expose no EF, `IQueryable`, WPF, broker SDK, or live-trading shortcut.

## Validation
Build; OperatorContracts Engine/integration tests; architecture tests; full tests; format.

## Completion Notes
Implemented immutable Engine-owned operator principals, authorities, resources, pagination, filters, summaries,
details, warnings, commands, progress, and stable query/command results for the complete Stage 7 surface. Added typed
query, Bot management, manual-run, Research, Proposal-decision, and kill-switch services over an intent-oriented
workflow port. `AuthorizedOperatorService` checks authority before every disclosure or mutation and maps both denied
and missing resources to `operator.unavailable`; all operations accept cancellation.

Validation:

- `./dev.ps1 build` — passed with 0 warnings and 0 errors.
- `./dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=OperatorContracts"` — 5 passed, 0 failed, 0 skipped.
- `./dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=OperatorContracts"` — 1 passed, 0 failed, 0 skipped.
- `./dev.ps1 test -Project tests/Trading.Architecture.Tests -Filter "Category=OperatorContracts"` — 2 passed, 0 failed, 0 skipped.
- `./dev.ps1 test` — 1,156 passed, 0 failed, 4 skipped. The four skips are the Stage 7 scenarios explicitly staged by
  `S7-001` for activation in `S7-016`.
- `./dev.ps1 format` — passed.

No deviations, follow-up tasks, or ADRs.
