---
schema_version: 1
id: S1-014
title: Add Windows and Linux CI
stage: 1
status: done
priority: 700
type: infrastructure
depends_on: [S1-005]
labels: [ci, windows, linux, quality-gate]
created: 2026-08-19
updated: 2026-08-19
---

# S1-014: Add Windows and Linux CI

## Objective

Automate Stage 1 restore, build, formatting, architecture, unit, and non-UI BDD validation on supported platforms.

## Scope

- Add Windows and Linux CI jobs for `net10.0` projects.
- Add the Windows WPF build.
- Run locked restore, Release build, format verification, unit tests, architecture tests, and Stage 1 non-UI Reqnroll tests.
- Publish test results and useful failure diagnostics.
- Enable dependency and secret scanning where supported by the chosen CI platform.

## Out of Scope

- WPF UI automation.
- Deployment and release packaging.
- Live external-provider tests.

## Acceptance Criteria

- A clean commit runs the required Windows and Linux jobs.
- Non-WPF projects build and test on both platforms.
- WPF builds on Windows.
- A failing test, formatting violation, analyzer warning, or architecture violation fails CI.
- Test results remain visible after failure.
- No CI step requires production secrets.

## Validation

- Run the workflow on a clean branch or equivalent CI validation environment.
- Confirm a temporary controlled failure makes the expected job fail, then remove it.

## Completion Notes

Implementation and hosted validation are complete.

- Added `.github/workflows/ci.yml` with independent Windows and Linux matrix entries. Both perform locked restore, a warning-as-error Release solution build, format verification, unit tests, architecture tests, and Stage 1 non-UI Reqnroll tests. Windows also performs an explicit native WPF build.
- Test commands emit TRX files. The artifact upload uses `always()` so completed test results and diagnostics remain available when a later validation step fails; test steps also continue after another test or format step fails when the build itself succeeded.
- Added `.github/workflows/security.yml` with pull-request dependency review and full-history Gitleaks secret scanning. The workflows receive only read-only repository access and the automatically supplied `GITHUB_TOKEN`; no production credentials are used.
- Refreshed the two isolated build-convention fixture lock files after current test-package conventions made the prior locks stale. No production dependency was changed.
- Local validation passed: `./dev.ps1 restore`; `./dev.ps1 build` (zero warnings and errors, including the cross-targeted WPF project); `./dev.ps1 format`; `./dev.ps1 test -Project tests/Trading.Core.Tests/Trading.Core.Tests.csproj` (275 passed); `./dev.ps1 test -Project tests/Trading.Architecture.Tests/Trading.Architecture.Tests.csproj` (6 passed); `./dev.ps1 test -Project tests/Trading.AcceptanceTests/Trading.AcceptanceTests.csproj -Filter "TestCategory=stage1&TestCategory!=windows"` (1 passed, 46 intentionally pending scenarios skipped); `./dev.ps1 test` (282 passed, 47 intentionally pending scenarios skipped); `./dev.ps1 solution-list`; and `./dev.ps1 reference-list`.
- Controlled local failure validation passed through `./dev.ps1 verify-build-conventions`: the compiler-warning fixture failed with `CS1030`, and the cross-platform fixture failed with `CA1416`, proving both violations propagate a failing command after their temporary fixture inputs are removed from the production build.
- Static inspection confirmed both runner matrix entries, every required gate, the Windows-only WPF condition, failure-independent TRX upload, dependency review, full-history secret scanning, and read-only permissions. A dedicated workflow linter was unavailable locally.
- Hosted validation used the temporary public branch `codex/s1-014-ci-validation-20260819`. The initial run exposed a missing Git line-ending contract: Linux passed, while Windows formatting failed although its build, tests, native WPF build, and artifact upload passed. Added `.gitattributes` to enforce LF checkout consistently, then observed clean Windows and Linux success in [run 32256407169](https://github.com/alistairshaw/llm-trader/actions/runs/32256407169).
- Controlled-failure validation passed. An analyzer-invalid probe caused both builds to fail with warnings treated as errors. The final runtime-only probe compiled successfully and [run 32257196378](https://github.com/alistairshaw/llm-trader/actions/runs/32257196378) failed both Windows and Linux at the unit-test step; architecture tests continued, artifact upload succeeded, and the API reported two non-expired artifacts: `test-results-Windows` and `test-results-Linux`.
- Removed the controlled failure and observed final clean success in [CI run 32257421821](https://github.com/alistairshaw/llm-trader/actions/runs/32257421821): locked restore, Release build, formatting, unit tests, architecture tests, and Stage 1 non-UI acceptance tests passed on Windows and Linux; native WPF build passed on Windows; artifact publication passed on both. [Security run 32257421910](https://github.com/alistairshaw/llm-trader/actions/runs/32257421910) passed without production secrets.
- Deviations: the remote repository initially contained only its initial commit, so the complete Stage 1 workspace was published to the uniquely named validation branch with explicit user approval. The branch remains available as the validated clean head; `main` was not changed.
- Follow-up tasks: none. `S1-015` is now ready.
- ADRs: none.
