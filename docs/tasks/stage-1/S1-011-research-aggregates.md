---
schema_version: 1
id: S1-011
title: Implement Research aggregates
stage: 1
status: done
priority: 740
type: feature
depends_on: [S1-006, S1-008]
labels: [domain, research, hypothesis]
created: 2026-08-19
updated: 2026-08-19
---

# S1-011: Implement Research Aggregates

## Objective

Implement the Stage 1 domain classes and lifecycle rules for Research Requests, Reports, and Hypotheses.

## Scope

- Implement `ResearchRequest` and `ResearchSubscription`.
- Implement immutable `ResearchReport` versions and provenance value objects.
- Implement `Hypothesis` and immutable `HypothesisVersion` lifecycle.
- Encode visibility, publication, versioning, freshness, and freeze/test transition invariants.

## Out of Scope

- Research Bot runtime.
- Web retrieval and source validation.
- Report persistence.
- Deterministic backtesting engine.

## Acceptance Criteria

- Research questions must be bounded and non-empty.
- Private visibility cannot be broadened after private inputs exist.
- Only authorized subscribers can be attached.
- Published Reports cannot be edited; revisions create versions.
- Report provenance and generator metadata are explicit value objects.
- Frozen Hypothesis versions cannot change.
- Changes after freezing require a new version.
- Lifecycle and visibility invariants have positive and negative unit tests.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=ResearchAggregates"
```

## Completion Notes

Implemented `ResearchRequest` and owned subscriptions with bounded questions, deterministic subscriber authorization,
private-input visibility narrowing, and published-result lifecycle rules. Added immutable `ResearchReport` versions with
explicit source provenance, generator metadata, freshness, and non-destructive dispositions. Added versioned
`Hypothesis` lifecycle behavior with immutable frozen specifications and guarded testing outcomes.

Validation performed in the repository Linux development container:

- `.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=ResearchAggregates"` — passed, 12 tests.
- `.\dev.ps1 build` — passed in Release, 0 warnings and 0 errors.
- `.\dev.ps1 test` — passed: 128 Core tests, 6 architecture tests, and 1 acceptance test; 47 intentionally deferred
  acceptance scenarios skipped.
- `.\dev.ps1 format` — passed with no output. The sandboxed invocation could not read Docker Desktop configuration,
  so the same repository command was rerun with approved Docker configuration access.

The first focused-test invocation occurred before rebuilding and reported no matching tests because the wrapper uses
the existing Release output; the Release build and repeated focused invocation above supplied the authoritative result.

Deviations: none. Follow-up tasks: none. ADRs: none.
