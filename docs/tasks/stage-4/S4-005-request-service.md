---
schema_version: 1
id: S4-005
title: Implement authorized request deduplication and reuse
stage: 4
status: planned
priority: 870
type: feature
depends_on: [S4-004]
labels: [research, authorization, deduplication, freshness]
created: 2026-08-20
updated: 2026-08-20
---

# S4-005: Implement Authorized Request Deduplication and Reuse

## Objective

Accept bounded authorized requests and deterministically reuse, subscribe, or enqueue Research work.

## Context

Use [Research Bot — Research Request](../../research-bot.md#4-research-request), [Deduplication and Reuse](../../research-bot.md#5-deduplication-and-reuse), [Versioning, Freshness, and Expiration](../../research-bot.md#11-versioning-freshness-and-expiration), and [Data Model — Research Requests](../../data-model.md#81-research_requests).

## Scope

- Validate requester identity, bounded question, normalized subject, requested sections, required source types, as-of time, visibility, private inputs, freshness, budget, and source-access policy.
- Construct a canonical normalized key from subject identity, normalized question, sections, source types, as-of/cutoff policy, methodology, visibility/private-input constraints, and report-schema version.
- Atomically return a sufficiently fresh authorized report, subscribe to one equivalent in-flight request, or create one queued request and initial subscription.
- Support explicit refresh requests linked to the existing report series and return stable decision and rejection codes.

## Acceptance Criteria

- Equivalent concurrent requests create one queued request and one subscription per authorized Bot.
- A sufficiently fresh equivalent authorized report is returned without a new request or Research run.
- Private inputs, visibility, freshness, methodology, source requirements, cutoff, or schema differences prevent unsafe reuse.
- Invalid or unauthorized input creates no request, subscription, or run and returns a stable code.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Research.Tests -Filter "Category=RequestService"
.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=ResearchDeduplication"
.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter "Category=ResearchRequests"
.\dev.ps1 build
```

## Completion Notes

Pending implementation.
