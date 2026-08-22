---
schema_version: 1
id: S7-008
title: Build Bot Run operations and status
stage: 7
status: ready
priority: 850
type: feature
depends_on: [S7-002, S7-005]
labels: [wpf, bot-run, scheduling]
created: 2026-08-22
updated: 2026-08-22
---
# S7-008: Build Bot Run Operations and Status

## Objective
Let operators trigger a Bot Run and inspect its status, outcome, usage, and diagnostics.

## Context
Use [BotRun Aggregate](../../domain.md#42-botrun-aggregate) and [Trading Bot](../../trading-bot.md).

## Scope
- Build manual-run command, active/queued/history views, trigger provenance, pinned inputs, budgets, usage, finish, schedule, and failure state.
- Surface coalesced, blocked, cancelled, recovered, and terminal outcomes without duplicate admission.
- Add accessibility metadata and view-model/application integration tests.

## Out of Scope
None.

## Acceptance Criteria
- A manual action creates one durable trigger and observable run outcome.
- Authoritative state, failure code, usage, and accepted schedule are visible.
- Cancelling navigation never cancels the durable run.

## Validation
Build; BotRuns WPF tests; OperatorBotRuns integration tests; full tests; format.

## Completion Notes
Pending implementation.
