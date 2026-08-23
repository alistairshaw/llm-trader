---
schema_version: 1
id: S7-022
title: Authorize deterministic WPF Research fixture identities
stage: 7
status: done
priority: 988
type: defect
depends_on: [S7-019, S7-021]
labels: [wpf, research, authorization, test-profile]
created: 2026-08-22
updated: 2026-08-22
owner: s7-022
---
# S7-022: Authorize Deterministic WPF Research Fixture Identities

## Objective
Authorize exact deterministic Research Report and series queries in the published WPF test profile.

## Context
Use [Architecture](../../architecture.md), [Research Bot](../../research-bot.md),
[Test Plan](../../test-plan.md), and [Local Development](../../local-development.md).

Hosted S7-017 candidate `70764b0fd9c2d2b2c83786e052bb8a824d904105` published
`operator.unavailable` from the production exact-version loader in Windows CI run `32612100996`. The production test
profile authorization resource set omits the deterministic Research Report ID `01J5QH8M000000000000000701` and its
series identity `fixture-series`.

## Scope
- Add the deterministic Research Report and series identities to the WPF test-profile authorization scope.
- Authorize exact immutable Report detail and version-history queries for the production-composed operator principal.
- Add focused production-composed tests for the catalog, exact Report ID, and series version-history authorization
  sequence used by `ResearchCatalogViewModel`.
- Verify unauthorized principals and identities retain the stable non-disclosing `operator.unavailable` outcome.
- Keep the authorization additions active only in the deterministic WPF test profile.

## Acceptance Criteria
- The production-composed WPF test-profile principal can query Report `01J5QH8M000000000000000701` with
  `exact:fixture-series:1` and query `fixture-series` version history.
- The returned exact Report matches the requested ID, series, and version 1.
- Production composition tests cover the authorized sequence and denied identities.
- Default and non-test profiles do not gain Research Report authority.

## Validation
Build; focused production composition and Research operator tests; full tests; format; publish-wpf.

## Completion Notes
Completed on 2026-08-22.

- Added the deterministic Report ID and series ID to the bounded production authorization resource set used only by
  the WPF test profile. Default and non-test profiles retain no Research Report authority.
- Added production-composed coverage for the catalog, exact immutable Report, and series version-history sequence,
  including exact identity assertions and stable unavailable results for missing permissions, unknown identities, and
  the default profile.
- Validation passed:
  - `.\dev.ps1 build` — Release build succeeded with zero warnings and zero errors.
  - `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter
    "FullyQualifiedName~OperatorProductionCompositionTests"` — 4 passed, zero failed or skipped.
  - `.\dev.ps1 test -Project tests/Trading.IntegrationTests -Filter
    "FullyQualifiedName~OperatorResearchTests"` — 2 passed, zero failed or skipped.
  - `.\dev.ps1 test` — 1,233 passed, zero failed or skipped.
  - `.\dev.ps1 format` — passed with no changes required.
  - `.\dev.ps1 publish-wpf` — self-contained `win-x64` publish succeeded.
- No deviations, follow-up tasks, or ADR changes.
