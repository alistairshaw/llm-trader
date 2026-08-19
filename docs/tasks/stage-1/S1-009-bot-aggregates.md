---
schema_version: 1
id: S1-009
title: Implement Trading Bot and Bot Run aggregates
stage: 1
status: done
priority: 760
type: feature
depends_on: [S1-007, S1-008]
labels: [domain, bot, lifecycle]
created: 2026-08-19
updated: 2026-08-19
---

# S1-009: Implement Trading Bot and Bot Run Aggregates

## Objective

Implement the domain classes, behavior, and invariants for Trading Bots, configuration versions, Bot Runs, triggers, and tool invocations.

## Scope

- Implement `TradingBot` and owned `TradingBotConfigurationVersion`.
- Implement `BotRun`, `BotRunTrigger`, and `ToolInvocation`.
- Encode lifecycle transitions, configuration immutability, lease state, trigger recording, terminal outcomes, and requested/accepted schedule separation.
- Publish domain events where Stage 1 behavior requires them.

## Out of Scope

- Durable lease storage.
- Actual scheduling or LLM execution.
- Repository implementations.

## Acceptance Criteria

- A Trading Bot has at most one active configuration version.
- Historical configuration content cannot change.
- Every Bot Run pins one configuration version.
- Terminal runs cannot resume.
- Only the scheduler-facing behavior can accept a next-run time.
- Tool invocations follow valid state transitions and are append-only facts after completion.
- Allowed and forbidden lifecycle transitions have table-driven unit tests.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=BotAggregates"
```

## Completion Notes

- Implemented `TradingBot` and immutable `TradingBotConfigurationVersion` content with sequential versions, single-active-version enforcement, historical supersession, portfolio assignment, enable/pause behavior, and explicit promotion before `LiveTrading`.
- Implemented `BotRun`, `BotRunTrigger`, and `ToolInvocation` with pinned configuration/snapshot identities, trigger de-duplication, lease ownership/renewal, explicit terminal outcomes, requested-versus-scheduler-accepted run times, and append-only completed tool facts.
- Added table-driven positive and forbidden lifecycle coverage under the `BotAggregates` category, including every terminal Bot Run outcome and both Tool Invocation outcomes.
- Validation: `.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=BotAggregates"` passed 15 tests; `.\dev.ps1 build` succeeded in Release with 0 warnings and 0 errors; `.\dev.ps1 test` passed 118 tests (111 Core, 6 architecture, 1 acceptance) with 47 intentionally deferred Stage 1 acceptance scenarios skipped; `.\dev.ps1 format` passed with no output.
- The first focused invocation used the prior built assembly and reported no matching category; after the required Release build, the exact command passed. The first formatter attempt was blocked from Docker Desktop configuration by the filesystem sandbox and passed unchanged when rerun with approved access.
- No scope deviations, follow-up tasks, domain events, or ADRs were required. Stage 1 does not yet define a concrete event contract or event-consuming Stage 1 behavior for these aggregates.
- Git working-tree inspection was unavailable because the workspace exposes no usable Git repository metadata; edits were kept to this task's domain, test, and task-tracking files.
