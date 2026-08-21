# Stage 5 Review Record

## Decision

Stage 5 satisfies its complete local acceptance gate after `S5-016` repaired the Windows SQLite resource lifecycle. The resumed `S5-015` exact-revision hosted Windows, Linux, and security gate is pending, and Stage 6 commencement remains withheld.

## Reviewed Scope

The review audited all Stage 5 tasks and exit criteria against production code, deterministic tests, migrated SQLite evidence, application-facing acceptance coverage, the headless demonstration, and the authoritative documentation. It covered structured proposal authority, exact Bot Run/configuration/Portfolio/snapshot/evidence binding, monotonic platform/account/Portfolio/Bot policies, immutable evaluations, actor authorization, fresh-state revalidation, atomic reservation and release behavior, ResearchOnly non-executability, authorized projections, audit reconstruction, recovery, and the absence of order or broker-submission authority.

The Stage 5 acceptance steps remain thin and delegate to the scenario-scoped `Stage5GovernanceDriver`. The driver composes the production Generic Host with deterministic substitutes and a fresh migrated file-backed SQLite database, then observes application projections and durable governance records. Focused Core, Engine, Data, Integration, and Architecture suites supply the lower-level positive, negative, concurrency, persistence, and authority evidence for each scenario family.

## Local Revision and Validation

The original local implementation validation was performed from `c56bee7b48f07edc798a233095187719249bc446`. The complete gate was repeated from SQLite lifecycle repair `f00f99434106624503886dd4cf0bb5678b762cc6`; the subsequent review commit changes documentation only and is the exact revision that must pass hosted validation.

All commands ran through the repository's Linux Docker workflow on 2026-08-20:

| Command | Result |
| --- | --- |
| `.\dev.ps1 restore` | Passed in locked mode; all projects were up to date. |
| `.\dev.ps1 build` | Passed; Release build produced 0 warnings and 0 errors. |
| `.\dev.ps1 format` | Passed with no formatting changes required. |
| `.\dev.ps1 test -Project tests/Trading.Architecture.Tests` | 19 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.Core.Tests` | 491 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.Data.Tests` | 149 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.Research.Tests` | 56 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.Engine.Tests` | 92 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.IntegrationTests` | 25 passed, 0 failed, 0 skipped. |
| `.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"` | Passed twice consecutively; each run had 32 passed, 0 failed, 0 pending, and 0 skipped. |
| `.\dev.ps1 test` | 997 passed, 0 failed, 0 skipped across all projects. |
| `.\dev.ps1 test -Project tests/Trading.Data.Tests -Filter "Category=Stage5Migrations\|Category=ProposalPersistence"` | 5 passed, covering fresh migration, completed-Stage-4 upgrade, retained history, schema equivalence, constraints, immutability, exact values, concurrency, and drift. |
| `docker compose run --rm --no-deps dev bash -lc "dotnet tool restore >/dev/null && dotnet ef migrations has-pending-model-changes --project src/Trading.Data"` | Passed; no pending model changes. |
| `.\dev.ps1 run` | Passed twice from a rebuilt smoke database with reproducible Stage 5 business identities, hashes, decisions, and shutdown results. |

After the Windows teardown repair, the entire matrix above was repeated successfully. Updated focused counts were Architecture 19, Core 491, Data 149, Research 56, Engine 93, Integration 27, and Acceptance 165. `FixtureDisposal` passed 2/2, covering immediate first-attempt deletion after scoped-provider and full-host lifecycles, and the `MultiBotSupervisor` lifecycle selection passed 8/8, including repeated asynchronous disposal. Stage 5 acceptance passed twice at 32/32 with zero skipped, and the full suite passed 1000/1000 with zero skipped. Fresh/Stage-4-upgrade migrations passed 5/5, EF drift remained clean, formatting passed, and the headless smoke reproduced the identities and hashes below.

The Stage 5 migration chain is `20260820211945_AddStage5ProposalGovernance`, `20260820221702_AddImmutableGuardrailEvaluationArtifacts`, and `20260820222346_RestoreGuardrailEvaluationImmutabilityTriggers`, upgrading from Stage 4 migration `20260820164929_AddStage4ResearchPersistence`.

## Deterministic Demonstration Evidence

Both headless executions reproduced:

- Proposal `01J5QH8M000000000000000401`, content hash `aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa`.
- Initial evaluation hash `dfa7fa03e563744be5beca6cac195989eb5348d08950bdd445ebcd8f0a6473b5` and fresh evaluation hash `0ac55f8861006267665e6ee2920a9fef5273f64da8dae258a3cfd0cf32b91334`.
- 44 hierarchical rule results and reservation `01J5QH8M000000000000000701` for exactly `700 USD`.
- Competing outcome `proposal_governance.insufficient_capital` and invalid proposal `01J5QH8M000000000000000403` with `proposal_governance.policy_rejected`.
- ResearchOnly proposal `01J5QH8M000000000000000404` with `proposal_governance.research_only`.
- Four authorized proposal projections, `700` active reserved total, zero broker submissions, and recoverable graceful shutdown.

Trading Bot Run identifiers and host-instance identifiers are operational attempt identities and intentionally differ between fresh smoke processes; Stage 5 business identities and canonical hashes are stable.

## Review Findings and Documentation

The review corrected one high-severity validation-gap before review: hosted CI previously exercised only Core, Architecture, and Stage 1 acceptance tests. The Windows/Linux job now executes the complete solution test suite, including all Stage 5 projects and all 32 acceptance cases. The README documentation map was also advanced from the Stage 4 backlog to Stage 5, and task/index metadata was reconciled.

The EF migration runner emits existing SQLite warnings for non-transactional `PRAGMA foreign_keys` operations and the staged guardrail-evaluation rebuild. Fresh and upgrade migration tests, immutable-trigger tests, full Data tests, repeated clean smoke migrations, and model-drift validation all pass. This is recorded as an operational diagnostic, not an unresolved data-integrity defect.

No ADR was created or changed. Hosted validation created follow-up defect task `S5-016`.

## Hosted Exact-Revision Gate

Revision `88681e512dcbde8f04a3e2865722f5646f0b073f` produced:

- Linux job `96622973099`: passed.
- Security run `32431213636`, secret-scan job `96622973233`: passed.
- Windows job `96622973230`: failed during test teardown.

Downloaded artifact `9429220872` shows recursive temporary-directory cleanup raising `IOException` because `test.db`, `runtime.db`, `smoke.db`, `workflow.db`, `capital.db`, `research.db`, and `recovery.db` remained open in another process. The breadth and consistent teardown signature establish a fixture/host resource-ownership defect rather than a Stage 5 business assertion failure.

`S5-016` made SQLite and host disposal Windows-safe without sleeps, skipped checks, cleanup retries, garbage-collection forcing, process-wide pool clearing, or swallowed failures. The repair gives each test lifecycle explicit asynchronous ownership, clears only the exact closed SQLite connection pool after all owners are disposed, deletes once immediately, and includes regressions for both scoped-provider and complete-host paths. Local review found no remaining resource-lifetime or documentation discrepancy.

The failed revision `88681e512dcbde8f04a3e2865722f5646f0b073f` and its Windows job are superseded as stage-gate evidence. The new review revision must pass the complete hosted Windows/Linux and security gates before this review can approve Stage 6.
