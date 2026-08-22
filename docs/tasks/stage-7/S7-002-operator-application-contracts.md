---
schema_version: 1
id: S7-002
title: Define authorized operator application contracts
stage: 7
status: planned
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
Pending implementation.

