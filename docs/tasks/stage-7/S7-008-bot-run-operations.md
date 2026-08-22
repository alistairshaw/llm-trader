---
schema_version: 1
id: S7-008
title: Build Bot Run operations and status
stage: 7
status: done
priority: 850
type: feature
depends_on: [S7-002, S7-005]
labels: [wpf, bot-run, scheduling]
created: 2026-08-22
updated: 2026-08-22
owner: /root/s7_008
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

- Added the Runs workspace with accessible manual-trigger controls, separate active/queued/history views, and
  authoritative run details for trigger provenance, pinned configuration and Portfolio snapshot, budgets, usage,
  finish outcome, requested and accepted schedule, stable failure code, and recovery state.
- Extended the authorized operator read contract with durable queued-trigger and complete run-audit projections;
  wired the Runs route into the WPF navigation factory and application service composition.
- Manual submission deliberately stops using the page-lifetime cancellation token after validation so navigation can
  cancel observation and refresh work without cancelling durable trigger creation. Stable coalesced, blocked, and
  terminal outcomes remain visible through command codes and run state.
- Validation: `./dev.ps1 restore` passed in locked mode; `./dev.ps1 build` passed with zero warnings and errors;
  `./dev.ps1 test -Project tests/Trading.UI.Wpf.Tests/Trading.UI.Wpf.Tests.csproj -Filter
  'TestCategory=BotRuns'` passed 4/4; `./dev.ps1 test -Project
  tests/Trading.IntegrationTests/Trading.IntegrationTests.csproj -Filter 'TestCategory=OperatorBotRuns'` passed 2/2;
  `./dev.ps1 test` passed 1,186 tests with the four previously declared pending Stage 7 acceptance scenarios skipped;
  `./dev.ps1 format` passed.
- The first build attempt in the fresh worktree correctly failed because restore outputs did not exist. After the
  required locked restore, one Docker client connection ended with `unexpected EOF`; the Linux engine remained healthy
  and the immediate complete build retry passed. No validation failure was hidden.
- Deviations: none. Follow-up tasks: none. ADRs: none.
