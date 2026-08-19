---
schema_version: 1
id: S3-008
title: Implement authorized Stage 3 tool dispatch
stage: 3
status: done
priority: 800
type: feature
depends_on: [S3-002, S3-007]
labels: [tools, authorization, schemas]
owner: s3_008
created: 2026-08-19
updated: 2026-08-19
---

# S3-008: Implement Authorized Stage 3 Tool Dispatch

## Objective

Validate, authorize, execute, and audit the `GetPortfolioSnapshot` and `Finish` tools.

## Context

Follow [Trading Bot — Tool Contract](../../trading-bot.md#8-tool-contract), [Finish](../../trading-bot.md#86-finish), and the authority restrictions in [Trading Bot](../../trading-bot.md#2-authority-boundary).

## Scope

- Register exactly `GetPortfolioSnapshot` schema version `1` and `Finish` schema version `1` in the production Stage 3 dispatcher.
- Validate canonical JSON arguments with strict required fields, types, unknown-field rejection, and size limits.
- Authorize each invocation against the pinned `ToolPolicy`, per-tool call limit, total tool-call budget, run identity, and cancellation state.
- Execute `GetPortfolioSnapshot` against the pinned input service.
- Execute `Finish` once with status, non-empty summary, and paired optional UTC `nextRunAt` and wake reason.
- Persist invocation start and terminal audit facts with canonical arguments/results, usage, duration, and normalized redacted errors.
- Reject unknown tools, unsupported schema versions, malformed arguments, repeated `Finish`, and calls after `Finish`.

## Acceptance Criteria

- Only both registered tools can execute.
- A registered tool absent from the pinned policy is rejected and audited.
- Every malformed or unauthorized call produces a stable result code without executing the tool.
- `Finish` closes the loop and returns a typed `FinishResult` without approving or creating any financial action.
- Audit records contain no credentials, provider secrets, or unbounded payloads.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ToolDispatch"
.\dev.ps1 build
```

## Completion Notes

Implemented the version-1 Stage 3 tool registry and authorized dispatcher for exactly
`GetPortfolioSnapshot` and `Finish`. The dispatcher loads the run's pinned configuration,
enforces run and snapshot identity, cancellation, per-tool and total call budgets, strict
canonical JSON schemas, unknown-field rejection, and bounded arguments. It returns stable
result codes for every validation and authorization outcome, executes only the pinned snapshot
service, and records a one-shot typed `FinishResult` without creating financial actions.

Every attempted invocation made while the run is accepting a tool is persisted first and then
completed with canonical bounded results, cumulative usage, elapsed duration, or a normalized
redacted error code. Oversized payloads are replaced with a bounded audit marker. Added Engine
coverage for registry contents, successful execution, all validation and policy denials,
cancellation, budgets, identity isolation, redaction, repeat/post-finish handling, and audit
facts, plus a SQLite repository round-trip test for invocation start and terminal facts.

Validation completed on 2026-08-19:

- `.\dev.ps1 test -Project tests/Trading.Engine.Tests -Filter "Category=ToolDispatch"` — passed 11, failed 0, skipped 0.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=BotRuntimePersistence|Category=Stage3Migrations"` — passed 12, failed 0, skipped 0; migration/model-drift coverage passed.
- `.\dev.ps1 build` — succeeded with 0 warnings and 0 errors.
- `.\dev.ps1 test` — passed 588, failed 0; 26 later Stage 3 acceptance scenarios remain intentionally skipped.
- `.\dev.ps1 format` — passed with no changes required.

No scope deviations, follow-up tasks, or ADRs were required.
