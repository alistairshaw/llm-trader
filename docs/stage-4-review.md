# Stage 4 Review Record

## Decision

Stage 4 passes its complete local review and is ready for exact-revision hosted validation. Stage 5 remains unapproved until the reviewed revision passes Windows, Linux, and security workflows.

## Delivered Scope

Stage 4 delivers bounded authorized Research requests; equivalent-request deduplication and fresh reuse; Shared, BotPrivate, and Restricted visibility; verified fixture-backed sources; strict Research tool dispatch; a budget-bounded scripted model loop; canonical citation-validated immutable report publication and refresh versioning; durable per-subscriber notification and source-keyed Trading Bot triggers; recoverable Research orchestration; pinned Trading Bot `RequestResearch`, `ListReports`, and exact-version `GetReport` tools; and shared cross-platform headless-host composition.

## Migration Identity

- Stage 2 baseline: `20260819154728_InitialStage2Persistence`
- Stage 3 runtime: `20260819220138_AddStage3BotRuntime`
- Stage 3 input audit: `20260819223000_AddBotRunInputRenderingHash`
- Stage 4 Research: `20260820164929_AddStage4ResearchPersistence`
- Stage 4 migration tests passed 5/5 for fresh creation and completed Stage 3 schema/data upgrade, including schema equivalence, retained history, constraints, immutable facts, and concurrency.
- EF model drift is empty.

## Acceptance-Criteria Audit

The criterion mapping is maintained in `tests/Trading.AcceptanceTests/Features/Research/TRACEABILITY.md`. All 39 Stage 4 scenarios passed twice consecutively with zero failed, pending, or skipped results.

The repaired acceptance boundary uses thin explicit feature-case routing. Its scenario-scoped driver composes the production host and executes production request decisions, deduplication, reuse, private visibility, catalog, publication, source retrieval, prompt-injection containment, Research tool authorization, bounded loop, notification, trigger delivery, recovery, shutdown, and Trading Bot tool-dispatch workflows. Assertions observe application results and migrated SQLite request, attempt, tool-audit, report, source, subscription, trigger, and Bot Run facts. No expected outcome is selected from a scenario title or assertion wording.

Focused Core, Data, Research, Engine, Integration, and Architecture tests supplement the end-to-end scenarios with lifecycle matrices, real-SQLite constraints and transactions, canonical audit reconstruction, exact provenance and citation validation, immutable versioning, authorization-before-catalog access, every Research budget boundary, durable idempotent delivery, abandoned-attempt terminalization and requeue, and exact-version Trading Bot consumption.

## Local Validation Evidence

Validated on 2026-08-20 through the repository Linux development container from Windows, from implementation revision `794f822` plus this review metadata:

| Command | Result |
| --- | --- |
| `.\dev.ps1 restore` | Passed in locked mode; all projects up to date. |
| `.\dev.ps1 build` | Passed in Release; 0 warnings, 0 errors. |
| `.\dev.ps1 format` | Passed; no formatter or analyzer findings. |
| `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` | 15 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.Core.Tests` | 391 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.Data.Tests` | 130 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.Research.Tests` | 56 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.Engine.Tests` | 57 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.IntegrationTests` | 23 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage4"` | Passed twice consecutively: 39 passed, 0 failed, 0 skipped each run. |
| `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage4Migrations\|Category=ResearchPersistence"` | 5 passed, 0 failed, 0 skipped; fresh and completed Stage 3 upgrade paths passed. |
| `docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"` | Passed; build succeeded and no pending model changes exist. |
| `.\dev.ps1 test` | 805 passed, 0 failed, 0 skipped (Core 391, Data 130, Research 56, Engine 57, Integration 23, Acceptance 133, Architecture 15). |
| `.\dev.ps1 run` | Passed; fresh migration, fixture-backed Trading and shared Research demonstration, recoverable shutdown, and deadline completed. |

## Runtime Demonstration and Audit Identities

The deterministic Docker smoke applied all migrations to a fresh isolated database and reported:

- Trading Bot Run `01M0GCBNA0A9P3HX6571PPWTK3`, outcome `Completed`.
- Bot Alpha `01J5QH8M000000000000000101`; Bot Beta `01J5QH8M000000000000000201`.
- Shared Report `01J5QH8M000000000000000501` with SHA-256 `c288b6f376c0e943d867dfa236417ecbd3b5dbc0c7362869a27d73c491d3db83`.
- First shared decision `Queued`; equivalent second decision `Subscribed`; initial shared runs `1`.
- Private request `Queued`; unauthorized private read denied.
- Refresh `Queued`; latest version `2`; latest Report `01J5QH8M000000000000000503`.
- Shutdown reported Research state recoverable, `CancelledRuns=0`, and `CompletedWithinDeadline=True`.

Research history is reconstructable from pinned request and attempt identities and versions, canonical bounded tool arguments/results/usage/timings/redacted errors, retrieved source identities and hashes, publication content hash and exact version, terminal results, subscription delivery, and source-keyed Bot triggers. The acceptance diagnostics include stable durable artifact identities and canonical business hashes.

## Documentation Audit

`README.md`, `AGENTS.md`, architecture, domain, data model, Research Bot, Trading Bot, test plan, implementation plan, local development, Stage 4 traceability, and task metadata were reconciled with the implementation and observed behavior.

- `README.md` accurately describes the explicit production-backed Stage 4 acceptance boundary.
- `AGENTS.md` and the authoritative documents accurately state the production composition, authority, isolation, persistence, canonical audit, provenance, recovery, deterministic-test, and thin-driver boundaries.
- No additional normative documentation, ADR, or follow-up task is required by the local review.

## Hosted Validation

Pending for the exact reviewed revision. Windows, Linux, and security workflows are the only remaining Stage 4 gates.

## Defects, Follow-ups, and Decisions

- The acceptance-driver defect identified in the initial review was corrected by `S4-016` in commit `794f822` and revalidated by the complete matrix above.
- Critical or high-severity defects: none known.
- Scope deviations: none.
- Follow-up tasks: none.
- ADRs created or changed: none.
- Stage 5 commencement: pending exact-revision hosted validation.
