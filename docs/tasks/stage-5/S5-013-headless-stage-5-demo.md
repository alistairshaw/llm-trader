---
schema_version: 1
id: S5-013
title: Demonstrate proposal governance in the headless host
stage: 5
status: done
priority: 720
type: feature
depends_on: [S5-011, S5-012]
labels: [host, smoke, proposals, demonstration]
created: 2026-08-20
updated: 2026-08-20
---

# S5-013: Demonstrate Proposal Governance in the Headless Host

## Objective

Extend the deterministic headless smoke to demonstrate Stage 5 proposal governance end to end.

## Context

Use [Implementation Plan — Stage 5 Demonstration](../../implementation-plan.md#7-stage-5-trade-proposals-approvals-and-risk), [Architecture — Trading.Host](../../architecture.md#66-tradinghost), and [Local Development — Repository Support](../../local-development.md#repository-support).

## Scope

- Compose Stage 5 services in the Generic Host using scripted model responses, fixture Research, deterministic market/account state, injected time and identifiers, and migrated SQLite.
- Demonstrate valid direct-trade and target-allocation proposals, an invalid proposal, structured hierarchical outcomes, an authorized approval, fresh revalidation, successful reservation, and a concurrent insufficient-capital result.
- Print bounded stable identities, versions, policy outcomes, reservation totals, and recoverable shutdown state.
- Keep the default smoke configuration simulated and broker-submission-free.
- Update README, architecture, local-development, test-plan, and AGENTS.md guidance when host behavior or commands change.

## Acceptance Criteria

- `.\dev.ps1 run` completes the Stage 3, Stage 4, and Stage 5 deterministic demonstrations from a fresh database.
- Durable SQLite facts match every printed proposal, evaluation, decision, and reservation identity and outcome.
- Repeated smoke execution starts from its dedicated clean smoke database and produces deterministic business hashes and stable outcomes.
- Graceful shutdown completes within the configured deadline with recoverable durable state.
- Host composition contains simulated inputs and no broker submission implementation.

## Validation

```powershell
.\dev.ps1 build
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=Stage5Host"
.\dev.ps1 run
.\dev.ps1 test
.\dev.ps1 format
```

## Completion Notes

Implemented the Stage 5 deterministic headless demonstration with validated Generic Host registrations, scripted
Trading sessions, fixture Research, paper-neutral account/instrument state, fixed governance time and identifiers,
fresh migrated SQLite snapshots, production guardrail/decision/reservation orchestration, authorized projections,
and no broker or order-submission implementation. The smoke records a valid direct proposal, a competing target
allocation, an oversized invalid proposal, and a ResearchOnly proposal. It prints bounded proposal, evaluation,
approval, reservation, projection, contention, shutdown, and zero-submission facts and verifies them against durable
state before stopping.

Stable smoke facts: proposal `01J5QH8M000000000000000401`, proposal hash
`aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa`, initial evaluation hash
`dfa7fa03e563744be5beca6cac195989eb5348d08950bdd445ebcd8f0a6473b5`, fresh evaluation hash
`0ac55f8861006267665e6ee2920a9fef5273f64da8dae258a3cfd0cf32b91334`, 44 rule results per complete
hierarchical evaluation, reservation `01J5QH8M000000000000000701` for `700 USD`, competing outcome
`proposal_governance.insufficient_capital`, invalid outcome `proposal_governance.policy_rejected`, ResearchOnly
outcome `proposal_governance.research_only`, four projected proposals, one active reservation totaling `700`, zero
broker submissions, and recoverable graceful shutdown.

Validation completed on 2026-08-20: `.\dev.ps1 build` passed with zero warnings/errors; focused
`Category=Stage5Host` passed 1/1; `Trading.IntegrationTests` passed 25/25; `.\dev.ps1 run` passed twice from its clean
database with identical business IDs, hashes, rule, reservation, contention, and safety outcomes; the full suite
passed 965 with the 32 intentionally pending S5-014 scenarios and no failures; focused `Category=Stage5Migrations`
passed 5/5 including EF model drift; and `.\dev.ps1 format` passed.

The implementation serializes the two deterministic smoke Bot runs because they intentionally share one host scope;
production concurrency continues to require independent scopes. README, AGENTS.md, architecture, local-development,
and test-plan guidance were updated. No scope deviations, follow-up tasks, or ADRs were created. Hosted Windows and
Linux validation remains delegated to S5-015.
