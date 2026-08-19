---
schema_version: 1
id: S1-004
title: Configure NUnit and Reqnroll test infrastructure
stage: 1
status: done
priority: 890
type: test
depends_on: [S1-003]
labels: [nunit, reqnroll, bdd]
created: 2026-08-19
updated: 2026-08-19
---

# S1-004: Configure NUnit and Reqnroll Test Infrastructure

## Objective

Make unit, architecture, and Gherkin acceptance tests discoverable through the standard .NET test command.

## Scope

- Configure NUnit for Stage 1 test projects.
- Configure Reqnroll with NUnit in `Trading.AcceptanceTests`.
- Add shared test conventions and scenario context support.
- Verify the Stage 1 feature files generate discoverable test cases.
- Establish `@stage1` and platform-tag filtering commands.

## Out of Scope

- Implementing all Stage 1 step bindings.
- FlaUI configuration, which begins with the WPF interface stage.
- Real external providers.

## Acceptance Criteria

- `dotnet test` discovers NUnit unit tests and Reqnroll scenarios.
- Stage 1 feature files compile as executable specifications.
- Tag filters can select `@stage1` and exclude `@windows` where required.
- Test dependencies do not appear in production project graphs.
- One minimal infrastructure scenario executes successfully.

## Validation

```powershell
.\dev.ps1 test
```

## Completion Notes

Completed 2026-08-19.

- Added centrally versioned NUnit, NUnit adapter/analyzers, .NET test SDK, and Reqnroll NUnit dependencies with refreshed lock files. Shared test-project conventions live in `tests/Directory.Build.props`; production projects retain empty package dependency graphs.
- Configured `Trading.AcceptanceTests` to generate NUnit cases from every Stage 1 feature. Existing domain scenarios are explicitly `@ignore` until their bindings are completed by their implementing tasks and `S1-015`, while a bound infrastructure scenario verifies scenario-scoped state and `ScenarioContext` support.
- Added discoverable NUnit smoke tests to the Core and Architecture test projects and documented `@stage1` plus cross-platform `@windows` exclusion filters in `docs/local-development.md`.
- Validation passed: one-time lock refresh with `docker compose run --rm --no-deps dev dotnet restore TradingBot.sln --force-evaluate -p:RestoreLockedMode=false`; `.\dev.ps1 restore`; `.\dev.ps1 build` (0 warnings, 0 errors); `.\dev.ps1 test` (3 passing infrastructure tests, 47 explicitly skipped deferred scenarios); both documented Stage 1 filters (48 selected versus 47 after excluding the single Windows scenario); `.\dev.ps1 format`; `.\dev.ps1 reference-list`; and restored production-graph inspection (zero package libraries and zero direct package dependencies).
- No scope deviations, follow-up tasks, or ADRs were required. The workspace exposes no `.git` metadata, so working-tree status and diff inspection were unavailable; changes were preserved through direct scoped inspection.
