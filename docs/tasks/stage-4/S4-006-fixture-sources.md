---
schema_version: 1
id: S4-006
title: Implement fixture-backed approved research sources
stage: 4
status: done
priority: 850
type: feature
depends_on: [S4-002]
labels: [research, fixtures, provenance, security]
created: 2026-08-20
updated: 2026-08-20
owner: s4_006
---

# S4-006: Implement Fixture-Backed Approved Research Sources

## Objective

Provide deterministic approved-source search and retrieval with complete provenance and an explicit untrusted-content boundary.

## Context

Use [Research Bot — Tooling](../../research-bot.md#7-tooling), [Untrusted Content and Prompt Injection](../../research-bot.md#8-untrusted-content-and-prompt-injection), [Architecture — Security](../../architecture.md#12-security-and-trust-boundaries), and [Test Plan — Test Doubles](../../test-plan.md#53-test-doubles).

## Scope

- Implement versioned fixture search and document-retrieval providers with deterministic queries, ordering, failures, and cancellation.
- Return source type, stable identifier or URI, title, publisher, publication/effective time, retrieval time, licensing/retention metadata, byte count, and SHA-256 content hash.
- Delimit retrieved material as untrusted evidence and include fixtures containing instructions that attempt to change prompts, tools, visibility, budgets, credentials, and agent policy.
- Keep fixture manifests, payloads, and expected hashes versioned and platform-independent.

## Acceptance Criteria

- Identical fixture requests return identical ordered metadata and content hashes on Windows and Linux.
- Source content cannot invoke tools, access secrets or local configuration, read unrelated files, or alter any trusted policy field.
- Unsupported sources, oversized documents, provider failures, and cancellation return bounded stable results.
- Default tests and host smoke perform no network request.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Research.Tests -Filter "Category=FixtureSources|Category=PromptInjection"
.\dev.ps1 test -Project tests/Trading.Architecture.Tests
.\dev.ps1 build
.\dev.ps1 format
```

## Completion Notes

- Added the embedded `approved-fixtures` v1 manifest and deterministic regulatory-filing and adversarial publisher-commentary payloads. Loading verifies exact UTF-8 byte counts and lowercase SHA-256 hashes before the provider can serve either payload.
- Added deterministic, point-in-time search and bounded document retrieval with complete provenance, stable ordering, injected retrieval time, explicit untrusted-evidence delimiters, and stable result codes for unsupported providers, missing and oversized documents, provider failure, and cancellation.
- Extended Research source results with source type, title, publisher, effective time, and byte count. Added source-contract and prompt-injection boundary coverage that performs no network request.
- Updated `README.md`, `docs/research-bot.md`, and `docs/test-plan.md` with the implemented fixture, provenance, trust-boundary, and test-double contracts.
- Validation: `./dev.ps1 build` passed with 0 warnings and 0 errors; focused `FixtureSources|PromptInjection` tests passed 6/6; Research tests passed 20/20; architecture tests passed 15/15; the full suite passed 711 with 39 intentionally pending Stage 4 acceptance scenarios and 0 failures; `./dev.ps1 format` passed; `git diff --check` passed.
- Deviations: none. Follow-up tasks: none. ADRs: none.
