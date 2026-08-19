---
schema_version: 1
id: S3-006
title: Implement durable trigger ingestion and coalescing
stage: 3
status: ready
priority: 840
type: feature
depends_on: [S3-004, S3-005]
labels: [triggers, scheduler, coalescing]
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

Not completed.
