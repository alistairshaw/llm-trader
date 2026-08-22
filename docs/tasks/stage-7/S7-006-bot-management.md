---
schema_version: 1
id: S7-006
title: Build Trading Bot management
stage: 7
status: ready
priority: 860
type: feature
depends_on: [S7-002, S7-005]
labels: [wpf, bots, configuration]
created: 2026-08-22
updated: 2026-08-22
---
# S7-006: Build Trading Bot Management

## Objective
Let authorized operators create, configure, assign, pause, resume, retire, and inspect Bots.

## Context
Use [TradingBot Aggregate](../../domain.md#41-tradingbot-aggregate) and [Trading Bot](../../trading-bot.md).

## Scope
- Build Bot list, form, detail, configuration history, Portfolio assignment, and lifecycle commands.
- Validate mandate, risk, tools, budgets, schedule, model, prompt, and execution mode through application contracts.
- Display immutable configuration identities and add confirmations for retirement and mode promotion.
- Add accessibility metadata and view-model tests for success, validation, authorization, concurrency, and cancellation.

## Out of Scope
None.

## Acceptance Criteria
- Commands produce exact durable Bot/configuration outcomes and refresh the view.
- Failed commands preserve input and expose stable actionable state.
- ResearchOnly, HumanApproval, and Paper modes are textually and programmatically distinct.

## Validation
Build; BotManagement WPF tests; OperatorBotManagement integration tests; full tests; format.

## Completion Notes
Pending implementation.
