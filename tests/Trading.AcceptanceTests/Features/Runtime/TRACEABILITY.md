# Stage 3 Acceptance-Criteria Traceability

All Stage 3 scenarios are tagged `@stage3`, `@acceptance`, `@runtime`, and `@cross-platform`. Scheduling and recovery scenarios additionally use `@scheduling` and `@recovery`. The temporary `@ignore` tag marks implementation-dependent scenarios pending until `S3-014` binds and activates them. Scenario names are unique within Stage 3.

Run the discoverable Stage 3 specifications with:

```powershell
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage3"
```

| Stage 3 acceptance criterion | Feature and scenario(s) | Implementing task(s) |
| --- | --- | --- |
| All Stage 3 Reqnroll BDD scenarios run and pass on Windows and Linux. | Every Stage 3 scenario (`@cross-platform`) | `S3-014`, `S3-015` |
| Only one active run may exist for a particular Trading Bot. | `RunLifecycle.feature` — Enforce one active lease for a Trading Bot | `S3-003`, `S3-004`, `S3-010`, `S3-014` |
| Different Trading Bots may run concurrently within global resource limits. | `IsolationAndConcurrency.feature` — Run two isolated Trading Bots concurrently; Respect global runtime capacity | `S3-011`, `S3-014` |
| Triggers arriving during an active run are durably retained and coalesced. | `TriggerScheduling.feature` — Coalesce triggers retained during an active run | `S3-003`, `S3-004`, `S3-006`, `S3-010`, `S3-014` |
| Every run pins exactly one immutable Bot configuration version. | `RunLifecycle.feature` — Complete a manually triggered Bot Run; Start a scheduled Bot Run | `S3-002`–`S3-004`, `S3-007`, `S3-010`, `S3-014` |
| Every run receives an immutable reconciled Portfolio Decision Snapshot. | `PinnedInputAndTools.feature` — Render deterministic pinned run input; Return the pinned Portfolio Decision Snapshot | `S3-007`, `S3-008`, `S3-010`, `S3-014` |
| A tool call is rejected when absent from the pinned Tool Policy. | `PinnedInputAndTools.feature` — Reject a tool absent from the pinned Tool Policy | `S3-008`, `S3-014` |
| Time, token, cost, tool-call, research-request, and proposal budgets are enforced. | `BudgetsAndFailures.feature` — Stop when a run budget is exhausted (all examples) | `S3-002`, `S3-009`, `S3-014` |
| A malformed model response or missing `Finish` produces a safe terminal state. | `BudgetsAndFailures.feature` — Fail safely on a malformed model response; Fail safely when the model omits Finish | `S3-009`, `S3-010`, `S3-014` |
| A requested wake time is accepted, bounded, or rejected by deterministic scheduling policy. | `TriggerScheduling.feature` — Accept a requested wake time inside policy bounds; Bound a requested wake time outside policy bounds; Reject an invalid requested wake time | `S3-005`, `S3-010`, `S3-014` |
| The baseline schedule cannot be silently disabled by an LLM request. | `TriggerScheduling.feature` — all requested-wake-time scenarios; `BudgetsAndFailures.feature` — Fail safely when the model omits Finish | `S3-005`, `S3-009`, `S3-010`, `S3-014` |
| Restarting the host safely recovers expired leases. | `RecoveryAndHosting.feature` — Recover an expired run lease after restart | `S3-004`, `S3-012`–`S3-014` |
| One bot cannot access another bot's run context, artifacts, configuration, or Portfolio. | `IsolationAndConcurrency.feature` — Run two isolated Trading Bots concurrently; Reject cross-Bot run context access | `S3-007`, `S3-008`, `S3-011`, `S3-014` |
| The headless host starts configured bots and shuts down gracefully. | `RecoveryAndHosting.feature` — Start configured Bots in the headless host; Shut down the headless host gracefully | `S3-012`–`S3-014` |
| Every run can be reconstructed from its configuration, snapshot, tool calls, result, and schedule decision. | `RecoveryAndHosting.feature` — Reconstruct a completed run audit history; `RunLifecycle.feature` — Complete a manually triggered Bot Run | `S3-003`, `S3-004`, `S3-008`–`S3-010`, `S3-014` |
| Manual and scheduled triggers execute runs. | `RunLifecycle.feature` — Complete a manually triggered Bot Run; Start a scheduled Bot Run | `S3-006`, `S3-010`, `S3-014` |
| `Finish` records a terminal summary and requested scheduling input. | `PinnedInputAndTools.feature` — Finish a run with a terminal summary; `TriggerScheduling.feature` — requested-wake-time scenarios | `S3-008`–`S3-010`, `S3-014` |
| Failure paths leave durable state consistent. | `BudgetsAndFailures.feature` — failure scenarios; `RecoveryAndHosting.feature` — recovery and shutdown scenarios | `S3-009`, `S3-010`, `S3-012`, `S3-014` |

All names, identifiers, timestamps, model responses, budgets, and tool results are deterministic synthetic inputs. No scenario contacts an external model, public web service, market-data provider, broker, or wall clock.
