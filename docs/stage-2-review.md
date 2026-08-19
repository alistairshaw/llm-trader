# Stage 2 Review Record

## Decision

Stage 2 is approved and complete. Exact-revision local, Windows-hosted, Linux-hosted, and security validation passed. Stage 3 may begin.

## Delivered Scope

Stage 2 delivers SQLite and EF Core infrastructure, the initial schema migration, canonical persistence conversion, aggregate repositories and unit-of-work boundaries, Broker Connection and Broker Account persistence, Instrument identity and broker mappings, Trading Bot and immutable configuration-version persistence, Portfolio, Position, and append-only ledger persistence, immutable Portfolio Decision Snapshots, optimistic concurrency and transactional failure handling, no-tracking portfolio projections, and a restart-safe application-facing persistence workflow.

## Migration Identity

- EF Core migration: `20260819154728_InitialStage2Persistence`
- Migration history table: `__ef_migrations_history`
- Schema metadata version: `2`
- Previous-stage fixture: `tests/Trading.Data.Tests/Fixtures/stage1-empty.db`

## Acceptance-Criteria Traceability

The criterion-to-scenario and implementing-task matrix is maintained in `tests/Trading.AcceptanceTests/Features/Persistence/TRACEABILITY.md`. All 20 Stage 2 scenarios are active, cross-platform, and passed locally with zero pending or skipped results. Focused real-SQLite tests provide the relational evidence for conversions, repository mappings, ownership constraints, idempotency, corrections, immutability, concurrency, transactions, projections, migrations, and restricted deletes.

## Local Validation Evidence

Validated on 2026-08-19 through the repository's Linux development container from Windows:

| Command | Result |
| --- | --- |
| `.\dev.ps1 restore` | Passed in locked mode; all projects restored from committed lock files. |
| `.\dev.ps1 build` | Passed in Release; 0 warnings and 0 errors. |
| `.\dev.ps1 format` | Passed; no formatting changes required. |
| `.\dev.ps1 test -Project tests/Trading.Data.Tests` | Passed: 92, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.IntegrationTests` | Passed: 1, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage2"` | Passed: 20, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` | Passed: 11, failed: 0, skipped: 0. |
| `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Migrations"` | Passed: 3, failed: 0, skipped: 0; covers fresh creation, empty Stage 1 fixture upgrade, and idempotent reapplication. |
| `docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"` | Passed; no changes since the committed migration. |
| `.\dev.ps1 test` | Passed: 447, failed: 0, skipped: 0 (Core 275, Data 92, Integration 1, Acceptance 68, Architecture 11). |

## Restart and Hash Demonstration

`RestartSafePortfolioWorkflowTests.CompletePortfolioStateSurvivesAServiceProviderRestart` created and migrated a file-backed SQLite database, then persisted a paper Broker Connection, Broker Account, mapped Instrument, Trading Bot with active immutable configuration, Portfolio ownership, Position, ledger deposit, and immutable Decision Snapshot. It disposed the first `TradingDbContext`, opened a new context against the same database, and reloaded the domain aggregates plus no-tracking projections.

The reloaded state retained portfolio `01J5QH8M000000000000000005`, exact capital `10000.125 USD`, position quantity `12.34567890`, average cost `123.45678901 USD`, ledger source `DEP-100`, Trading Bot configuration identity `01J5QH8M000000000000000009`, and snapshot `01J5QH8M00000000000000000A`. The published and reloaded canonical snapshot hash was `8cfd7f682511c8b68fe8491b4c801c3734b72d4d300f01af954feaa8509813c2`.

## Hosted Validation

- Validated revision: `eb2eff0bcc4a726bae482369264449073c0e8d59`
- CI workflow: [run 32279052481](https://github.com/alistairshaw/llm-trader/actions/runs/32279052481) — passed.
- Linux job: [job 96153219013](https://github.com/alistairshaw/llm-trader/actions/runs/32279052481/job/96153219013) — passed; `test-results-Linux` artifact ID `9375128752`, 71,751 bytes.
- Windows job: [job 96153219267](https://github.com/alistairshaw/llm-trader/actions/runs/32279052481/job/96153219267) — passed; `test-results-Windows` artifact ID `9375162635`, 72,180 bytes.
- Security workflow: [run 32279052419](https://github.com/alistairshaw/llm-trader/actions/runs/32279052419) — passed.

## Defects, Follow-ups, and Decisions

- Critical or high-severity defects: none known.
- Scope deviations: none.
- Follow-up task IDs: none.
- ADRs created or changed: none.
- Stage 3 commencement: approved.
