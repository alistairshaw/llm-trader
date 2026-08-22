---
schema_version: 1
id: S7-006
title: Build Trading Bot management
stage: 7
status: done
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
Implemented an authorized Bot-management view model and accessible WPF workspace for Bot listing, creation,
configuration history identity display, Portfolio assignment, lifecycle commands, and explicit retirement or execution-
mode promotion confirmations. Commands retain form input on validation, authorization, and concurrency failures, expose
stable result codes, honor cancellation, and refresh summaries only after successful durable operator results. The WPF
navigation factory creates and disposes a fresh Bot workspace per visit and uses only explicitly registered operator
services and principal; production-backed application bindings remain assigned to `S7-016`.

Validation completed on 2026-08-22:

- `.\dev.ps1 restore -RefreshLocks` and `.\dev.ps1 restore` — passed; the WPF test lock records its Core and Engine references.
- `.\dev.ps1 build` — passed with 0 warnings and 0 errors.
- `.\dev.ps1 test -Project tests/Trading.UI.Wpf.Tests -Filter "Category=BotManagement"` — 7 passed, 0 failed, 0 skipped.
- `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=OperatorBotManagement"` — 2 passed, 0 failed, 0 skipped.
- `.\dev.ps1 test` — 1,180 passed, 0 failed, 4 expected pending Stage 7 scenarios skipped.
- `.\dev.ps1 format` — passed.

No deviations, follow-up tasks, or ADRs.
