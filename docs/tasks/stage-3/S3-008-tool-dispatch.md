---
schema_version: 1
id: S3-008
title: Implement authorized Stage 3 tool dispatch
stage: 3
status: ready
priority: 800
type: feature
depends_on: [S3-002, S3-007]
labels: [tools, authorization, schemas]
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

Not completed.
