# Stage 3 Review Record

## Decision

Stage 3 is approved and complete. Exact-revision local, Windows-hosted, Linux-hosted, and security validation passed. Stage 4 may begin.

## Delivered Scope

Stage 3 delivers immutable Trading Bot configuration pinning, Bot Run lifecycle and audit persistence, manual and scheduled durable triggers, per-bot leases, trigger coalescing, deterministic scheduling, reproducible pinned run inputs, strict authorization for the `GetPortfolioSnapshot` and `Finish` tools, a budget-bounded scripted model loop, complete single-run orchestration, capacity-bounded multi-bot supervision and isolation, expired-lease recovery, graceful shutdown, and a cross-platform Generic Host with deterministic simulated smoke mode.

## Migration Identity

- Stage 2 baseline migration: `20260819154728_InitialStage2Persistence`
- Stage 3 runtime migration: `20260819220138_AddStage3BotRuntime`
- Stage 3 input-audit migration: `20260819223000_AddBotRunInputRenderingHash`
- Previous-stage fixture: `tests/Trading.Data.Tests/Fixtures/stage2-completed.db`
- Migration tests: fresh database, completed Stage 2 fixture upgrade, and idempotent reapplication all passed.
- EF model-drift check: no pending model changes.

## Acceptance-Criteria Traceability

The criterion-to-scenario matrix is maintained in `tests/Trading.AcceptanceTests/Features/Runtime/TRACEABILITY.md`. All 26 Stage 3 scenarios are active, cross-platform, and passed locally with zero pending or skipped results. The matrix covers the full Stage 3 implementation-plan criteria, including one active run per bot, concurrent isolated bots, durable trigger coalescing, pinned configuration and snapshot input, tool policy, all six budgets, safe malformed/missing-`Finish` outcomes, deterministic wake scheduling, baseline preservation, recovery, graceful hosting, and complete audit reconstruction.

Focused Engine, Data, Integration, Architecture, migration, and headless-host tests supplement the application-facing scenarios at their lowest useful layers. All identifiers, clocks, responses, tool results, and budgets in commit-gating tests are deterministic synthetic inputs; no real model, public web, market data, broker, or live-money path is contacted.

## Local Validation Evidence

Validated on 2026-08-19 through the repository's Linux development container from Windows:

| Command | Result |
| --- | --- |
| `.\dev.ps1 restore` | Passed in locked mode; all projects up to date. The review first found and corrected the stale Acceptance project lock created when Engine and Host references were added. |
| `.\dev.ps1 build` | Passed in Release; 0 warnings and 0 errors. |
| `.\dev.ps1 format` | Passed; no formatting changes required. |
| `.\dev.ps1 test -Project tests/Trading.Core.Tests` | Passed: 367, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.Data.Tests` | Passed: 105, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.Engine.Tests` | Passed: 52, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.IntegrationTests` | Passed: 18, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` | Passed: 14, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage3"` | Passed: 26, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Migrations"` | Passed: 3, failed: 0, skipped: 0; covers fresh creation, completed Stage 2 fixture upgrade, and idempotent reapplication. |
| `docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"` | Passed; build succeeded and no model changes exist after the committed migrations. |
| `.\dev.ps1 run` | Passed; image built, both Stage 3 migrations applied to an isolated fresh database, smoke run `01M0E6X4WGN1D722J9DG72PCGJ` completed, and shutdown reported `CancelledRuns=0` and `CompletedWithinDeadline=True`. |
| `.\dev.ps1 test` | Passed: 650, failed: 0, skipped: 0 (Core 367, Data 105, Engine 52, Integration 18, Acceptance 94, Architecture 14). |

## Runtime Demonstration and Audit Reconstruction

The deterministic acceptance and focused runtime suites demonstrate two scripted bots executing concurrently within global capacity while each receives only its own configuration, run context, Portfolio, and immutable snapshot. They also demonstrate same-bot exclusion, retained/coalesced follow-up triggers, every budget terminal path, missing `Finish`, deterministic schedule acceptance/bounding/rejection, expired-lease recovery, lease-loss cancellation, and graceful supervisor shutdown.

The Docker smoke host seeded an enabled `ResearchOnly` bot and simulated Portfolio, migrated an isolated SQLite database, ingested a deterministic manual trigger, completed run `01M0E6X4WGN1D722J9DG72PCGJ`, and exited cleanly. Historical runs are reconstructable from the pinned configuration-version and snapshot identifiers, deterministic input-rendering version and SHA-256 hash, ordered trigger reasons, canonical model/tool invocation records and usage, terminal outcome and summary, requested/accepted schedule decision, lease history, and timestamps.

## Hosted Validation

- Validated revision: `59b32a578aa158c9094a83841d4583467a11d5a2`.
- CI workflow: [run 32315032234](https://github.com/alistairshaw/llm-trader/actions/runs/32315032234) — passed.
- Linux job: [job 96265412476](https://github.com/alistairshaw/llm-trader/actions/runs/32315032234/job/96265412476) — passed; `test-results-Linux` artifact ID `9387767044`, 89,267 bytes, not expired.
- Windows job: [job 96265412435](https://github.com/alistairshaw/llm-trader/actions/runs/32315032234/job/96265412435) — passed; `test-results-Windows` artifact ID `9387783692`, 89,613 bytes, not expired.
- Security workflow: [run 32315032334](https://github.com/alistairshaw/llm-trader/actions/runs/32315032334) — passed.
- Secret scan job: [job 96265412500](https://github.com/alistairshaw/llm-trader/actions/runs/32315032334/job/96265412500) — passed; `gitleaks-results.sarif` artifact ID `9387743432`, 6,772 bytes, not expired.
- Dependency review job: [job 96265413320](https://github.com/alistairshaw/llm-trader/actions/runs/32315032334/job/96265413320) — skipped as expected for a push event.

## Defects, Follow-ups, and Decisions

- Critical or high-severity defects: none known from local review.
- Review defect corrected: refreshed `tests/Trading.AcceptanceTests/packages.lock.json` to include the Stage 3 Engine and Host project dependency graph; locked restore then passed.
- Scope deviations: none.
- Follow-up task IDs: none.
- ADRs created or changed: none.
- Stage 4 commencement: approved.
