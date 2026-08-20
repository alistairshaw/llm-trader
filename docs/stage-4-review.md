# Stage 4 Review Record

## Decision

Stage 4 is not approved. Local build, test, migration, formatting, model-drift, and headless-smoke commands pass, but the Stage 4 Reqnroll driver derives outcomes from feature text instead of executing the required production Research workflows. `S4-016` must replace that driver behavior and the full review must be repeated before the reviewed revision is published for hosted Windows, Linux, and security validation.

## Delivered Scope Under Review

The production implementation includes bounded and authorized Research requests; equivalent-request deduplication and fresh reuse; Shared, BotPrivate, and Restricted visibility; fixture-backed approved sources; strict Research tool dispatch; a budget-bounded scripted model loop; canonical citation-validated immutable report publication and refresh versioning; durable per-subscriber notification and source-keyed Trading Bot triggers; recoverable Research orchestration; pinned Trading Bot `RequestResearch`, `ListReports`, and exact-version `GetReport` tools; and shared headless-host composition.

## Migration Identity

- Stage 2 baseline migration: `20260819154728_InitialStage2Persistence`
- Stage 3 runtime migration: `20260819220138_AddStage3BotRuntime`
- Stage 3 input-audit migration: `20260819223000_AddBotRunInputRenderingHash`
- Stage 4 Research migration: `20260820164929_AddStage4ResearchPersistence`
- Stage 4 migration tests passed 5/5 for fresh creation, completed Stage 3 schema/data upgrade, schema equivalence, retained history, constraints, immutable Research facts, and concurrency behavior.
- EF model-drift check reported no pending model changes.

## Acceptance-Criteria Audit

The criterion-to-scenario mapping is maintained in `tests/Trading.AcceptanceTests/Features/Research/TRACEABILITY.md`. Focused Stage 4 execution reports 39 passed, 0 failed, and 0 skipped. Focused Core, Data, Research, Engine, Integration, and Architecture suites also pass and cover request lifecycle and authority, real-SQLite persistence, catalog isolation, deduplication, tool schemas and budgets, fixture integrity and untrusted-evidence wrapping, publication and citation validation, notification idempotency, abandoned-attempt recovery, exact-version Trading Bot consumption, and host orchestration.

The acceptance audit found that `tests/Trading.AcceptanceTests/Support/Stage4ResearchDriver.cs` does not provide the production-backed evidence required by the implementation plan, test plan, and `S4-014`. Its action and assertion paths match step text and scenario titles, set expected values in an in-memory dictionary, and assert those values. Its application setup validates `TradingHostOptions` and migrates SQLite but does not invoke the production Research, Engine, notification, recovery, Trading Bot Research, or host workflows named by the scenarios. The 39 green Reqnroll results therefore cannot be used to approve the stage.

`S4-016` specifies the production-backed driver repair. Stage 4 traceability and the review matrix must be revalidated after that task is complete.

## Local Validation Evidence

Validated on 2026-08-20 through the repository's Linux development container from Windows, against production revision `550f944` plus the review-record metadata changes:

| Command | Result |
| --- | --- |
| `.\dev.ps1 restore` | Passed in locked mode; all projects restored. |
| `.\dev.ps1 build` | Passed in Release; 0 warnings and 0 errors. |
| `.\dev.ps1 format` | Passed; no formatter or analyzer findings. |
| `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` | Passed: 15, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.Core.Tests` | Passed: 391, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.Data.Tests` | Passed: 130, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.Research.Tests` | Passed: 55, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.Engine.Tests` | Passed: 57, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.IntegrationTests` | Passed: 23, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage4"` | Reported passed: 39, failed: 0, skipped: 0; review determined the driver does not yet prove production behavior. |
| `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage4Migrations\|Category=ResearchPersistence"` | Passed: 5, failed: 0, skipped: 0; covers fresh and completed Stage 3 upgrade paths. |
| `docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"` | Passed; build succeeded and no pending model changes exist. |
| `.\dev.ps1 test` | Passed: 804, failed: 0, skipped: 0 (Core 391, Data 130, Research 55, Engine 57, Integration 23, Acceptance 133, Architecture 15). |
| `.\dev.ps1 run` | Passed; the fixture-backed Trading and Research smoke completed and shut down within its deadline. |

## Runtime Demonstration and Audit Identities

The deterministic Docker smoke applied all four migration generations to a fresh isolated database and reported:

- Trading Bot Run: `01M0GA3T43QHEDX0YC034Z3Y79`, outcome `Completed`.
- Shared subscribers: Bot Alpha `01J5QH8M000000000000000101`; Bot Beta `01J5QH8M000000000000000201`.
- Shared Report: `01J5QH8M000000000000000501`.
- Shared canonical SHA-256: `c288b6f376c0e943d867dfa236417ecbd3b5dbc0c7362869a27d73c491d3db83`.
- Latest refresh version: `2`; latest Report `01J5QH8M000000000000000503`.
- Observed decisions: first shared request `Queued`, equivalent second request `Subscribed`, private request `Queued`, refresh `Queued`, initial shared runs `1`, and unauthorized private read denied.
- Shutdown: `CancelledRuns=0`, `CompletedWithinDeadline=True`; Research shutdown state reported recoverable.

Focused production tests reconstruct Research audit from pinned request and attempt identities, version pins, canonical bounded tool arguments/results/errors, deterministic usage and timings, retrieved-source hashes and provenance, terminal result codes, publication hashes and versions, subscriber delivery, and source-keyed Bot triggers. The acceptance driver defect does not alter those focused test results, but it prevents the Stage 4 business scenarios from serving as the required end-to-end proof.

## Documentation Audit

`README.md`, `AGENTS.md`, architecture, domain, data model, Research Bot, Trading Bot, test plan, implementation plan, local development guide, task metadata, and Stage 4 traceability were audited against the implementation and local validation behavior.

- `README.md` previously stated that all 39 cases already executed through the required application driver. It now records the `S4-016` production-binding gate.
- `AGENTS.md` and the authoritative documents already state the correct production-service, authority, persistence, provenance, recovery, deterministic-test, and driver boundaries; no normative change is required.
- The Stage 4 index now selects `S4-016`; `S4-015` is blocked on it.

## Hosted Validation

Not run for a reviewed Stage 4 revision. Windows, Linux, and security workflow evidence remains required after `S4-016` and the repeated local review pass.

## Defects, Follow-ups, and Decisions

- Stage-blocking defect: Stage 4 acceptance outcomes are derived from Gherkin text and preassigned driver state instead of production application workflow results and durable facts.
- Follow-up task: `S4-016` — Bind Stage 4 acceptance to production Research workflows.
- Critical or high-severity production defects found by the otherwise-passing focused and smoke gates: none known.
- Scope deviations: the review stopped before publication and hosted validation because the local acceptance-evidence gate is unsatisfied.
- ADRs created or changed: none.
- Stage 5 commencement: not approved.
