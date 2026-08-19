---
schema_version: 1
id: S3-006
title: Implement durable trigger ingestion and coalescing
stage: 3
status: done
priority: 840
type: feature
depends_on: [S3-004, S3-005]
labels: [triggers, scheduler, coalescing]
owner: s3_006
created: 2026-08-19
updated: 2026-08-19
---

# S3-006: Implement Durable Trigger Ingestion and Coalescing

## Objective

Accept runtime triggers durably and coalesce them into at most one eligible follow-up run per Bot.

## Context

Follow [Trading Bot — Triggers and Scheduling](../../trading-bot.md#5-triggers-and-scheduling) and [Data Model — Start a Bot Run](../../data-model.md#131-start-a-bot-run).

## Scope

- Implement application services for authorized manual, baseline, accepted-next-run, portfolio, and operational triggers available in Stage 3.
- Persist every trigger reason before acknowledging ingestion.
- Deduplicate triggers carrying the same Bot, source type, and source identity.
- Claim all pending eligible triggers for one Bot in deterministic occurrence/identity order.
- Coalesce triggers arriving during an active run into one pending follow-up claim while retaining each trigger record and reason.
- Re-evaluate Bot lifecycle and scheduling eligibility immediately before claim.
- Add concurrent real-SQLite integration tests for ingestion, deduplication, active-run arrival, deterministic coalescing, rollback, and separate-Bot independence.

## Acceptance Criteria

- Acknowledged triggers survive context and host restart.
- Duplicate sourced triggers create one durable record.
- No trigger reason is lost during coalescing.
- One Bot receives at most one follow-up run claim for all triggers accumulated during its active run.
- Different Bots claim triggers independently.
- Ineligible Bots retain unconsumed triggers without starting a run.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=TriggerCoalescing"
.\dev.ps1 build
```

## Completion Notes

Implemented application services for Stage 3-authorized manual, baseline, accepted-next-run,
portfolio-event, and risk/reconciliation trigger ingestion. Ingestion persists before returning an
accepted result and treats duplicate Bot/source-type/source-identity tuples as idempotent duplicate
acknowledgements. Added lifecycle, active-configuration, due-time, and pending-trigger rechecks
immediately before claim.

Updated transactional run claiming to consume only eligible triggers and to order coalesced reasons
by occurrence time and trigger identity. Triggers arriving during an active run remain pending and
form at most one follow-up run under the existing conditional active-run constraint.

Added real-SQLite integration coverage using fixed clocks and explicit async start gates for restart
durability, sourced deduplication, due-time eligibility, active-run arrivals, deterministic reason
retention, claim rollback, paused-Bot retention, same-Bot exclusion, and different-Bot independence.

Validation:

- `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=TriggerCoalescing"` — passed, 4 tests.
- `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=BotRuntimePersistence"` — passed, 5 tests.
- `.\dev.ps1 test -Project tests/Trading.IntegrationTests` — passed, 5 tests.
- `.\dev.ps1 build` — passed with zero warnings and zero errors.
- `.\dev.ps1 test` — passed, 570 tests; 26 intentionally deferred Stage 3 acceptance scenarios skipped.
- `.\dev.ps1 format` — passed.

Deviations: none. Follow-up tasks: none. ADRs: none.
