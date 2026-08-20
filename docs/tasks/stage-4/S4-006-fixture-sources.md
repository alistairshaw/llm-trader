---
schema_version: 1
id: S4-006
title: Implement fixture-backed approved research sources
stage: 4
status: ready
priority: 850
type: feature
depends_on: [S4-002]
labels: [research, fixtures, provenance, security]
created: 2026-08-20
updated: 2026-08-20
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

Pending implementation.
