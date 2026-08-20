---
schema_version: 1
id: S4-007
title: Implement authorized Research tool dispatch
stage: 4
status: done
priority: 830
type: feature
depends_on: [S4-005, S4-006]
labels: [research, tools, authorization, prompt-injection]
created: 2026-08-20
updated: 2026-08-20
---

# S4-007: Implement Authorized Research Tool Dispatch

## Objective

Validate, authorize, execute, and audit the complete fixture-backed Stage 4 Research tool set.

## Context

Use [Research Bot — Tooling](../../research-bot.md#7-tooling), [Authority Boundary](../../research-bot.md#2-authority-boundary), [Untrusted Content](../../research-bot.md#8-untrusted-content-and-prompt-injection), and [Test Plan — Security and Authorization Tests](../../test-plan.md#14-security-and-authorization-tests).

## Scope

- Register schema version `1` for `SearchWeb`, `FetchWebDocument`, `ListReports`, `GetReport`, `PublishReportDraft`, and `FinishResearch` using fixture-backed source implementations.
- Apply strict canonical JSON schemas, required types, unknown-field rejection, payload limits, pinned tool policy, per-tool and total budgets, document/byte limits, run identity, cancellation, and one-shot finish rules.
- Validate report access through the authorized catalog and validate draft citations against sources retrieved by the same run.
- Persist invocation start and terminal facts, canonical bounded arguments/results, provenance, usage, duration, and normalized redacted errors.
- Treat every retrieved content field as inert evidence throughout tool dispatch and audit rendering.

## Acceptance Criteria

- Only the six registered version-1 tools execute, and every denial returns a stable audited result without side effects.
- The production registry contains no proposal, approval, reservation, order, broker, configuration mutation, visibility broadening, arbitrary-code, or general-filesystem tool.
- Retrieved instructions cannot alter trusted prompts, tool permissions, visibility, budgets, schema, or agent policy.
- Repeated finish, post-finish calls, unknown versions, malformed/oversized input, unauthorized report reads, and exceeded budgets are rejected and audited without secrets or unbounded payloads.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Research.Tests -Filter "Category=ToolDispatch|Category=PromptInjection"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ResearchToolAudit"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 build
```

## Completion Notes

Completed on 2026-08-20. Added the exact version-1 Research registry for `SearchWeb`, `FetchWebDocument`, `ListReports`, `GetReport`, `PublishReportDraft`, and `FinishResearch`. The dispatcher enforces canonical closed JSON schemas, bounded arguments/results, the pinned attempt and tool-set identity, cancellation, per-tool/total/document/retained-byte budgets, authorized catalog reads, same-attempt citation provenance, and one-shot completion. Every success or denial appends bounded canonical start/terminal audit facts with duration, usage, stable result codes, and redacted error detail. The registry contains no trade, approval, reservation, order, broker, configuration, visibility-mutation, arbitrary-code, or filesystem authority.

Validation: `.\dev.ps1 build` passed with 0 warnings and 0 errors; `.\dev.ps1 test -Project tests/Trading.Research.Tests -Filter "Category=ToolDispatch|Category=PromptInjection"` passed 12; `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ResearchToolAudit"` passed 4; `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` passed 15; `.\dev.ps1 test` passed 722 with the 39 intentionally pending Stage 4 acceptance scenarios; `docker compose run --rm --no-deps dev dotnet format TradingBot.sln --no-restore` applied formatting because the wrapper intentionally exposes verification only; `.\dev.ps1 format` then passed; and `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage4Migrations"` passed 5 including empty EF model drift.

Updated the README, Research Bot authority documentation, and AGENTS.md durable audit guidance. No scope deviations, follow-up tasks, or ADR changes.
