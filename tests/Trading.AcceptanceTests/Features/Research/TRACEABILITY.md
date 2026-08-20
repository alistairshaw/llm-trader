# Stage 4 Acceptance-Criteria Traceability

All Stage 4 scenarios are tagged `@stage4`, `@acceptance`, `@research`, and `@cross-platform`. Recovery scenarios additionally use `@recovery`. Until `S4-014` supplies application-facing bindings, the acceptance harness's temporary `@ignore` tag makes every implementation-dependent scenario explicitly pending. Scenario names are unique within Stage 4.

Run the discoverable Stage 4 specifications with:

```powershell
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage4"
```

| Stage 4 acceptance criterion or deliverable | Feature and scenario(s) | Implementing task(s) |
| --- | --- | --- |
| All Stage 4 Reqnroll BDD scenarios run and pass on Windows and Linux. | Every Stage 4 scenario (`@cross-platform`) | `S4-014`, `S4-015` |
| Equivalent concurrent requests produce one Research Bot run. | `DeduplicationAndReuse.feature` — Deduplicate equivalent concurrent shared requests; Do not merge requests with different private inputs | `S4-004`, `S4-005`, `S4-011`, `S4-014` |
| A sufficiently fresh equivalent Report can satisfy a later request without another run. | `DeduplicationAndReuse.feature` — Reuse a sufficiently fresh equivalent Report; Refresh an expired equivalent Report | `S4-004`, `S4-005`, `S4-009`, `S4-014` |
| Private Reports remain inaccessible to unauthorized bots. | `PublicationAndVisibility.feature` — Enforce private Report visibility; `SharedReportJourney.feature` — Complete shared, private, and refreshed Research in the headless host | `S4-004`, `S4-009`, `S4-012`–`S4-014` |
| Visibility cannot be broadened after private inputs are provided. | `RequestAuthorization.feature` — Prevent visibility broadening after private input; `DeduplicationAndReuse.feature` — Do not merge requests with different private inputs | `S4-002`, `S4-005`, `S4-007`, `S4-009`, `S4-014` |
| Published Reports cannot be modified. | `PublicationAndVisibility.feature` — Publish a complete immutable Report; Reject mutation of a published Report | `S4-003`, `S4-004`, `S4-009`, `S4-014` |
| Refreshing a Report creates a new immutable version. | `PublicationAndVisibility.feature` — Publish a refresh as a new version; `SharedReportJourney.feature` — Complete shared, private, and refreshed Research in the headless host | `S4-009`, `S4-013`, `S4-014` |
| Reports include complete provenance and generation metadata. | `ProvenanceAndToolSafety.feature` — Preserve provenance for fixture-backed evidence; `PublicationAndVisibility.feature` — Publish a complete immutable Report | `S4-006`, `S4-007`, `S4-009`, `S4-014` |
| Partial or failed research is retained for audit but not published as completed. | `PublicationAndVisibility.feature` — Retain failed Research without publishing it; budget scenarios in `ProvenanceAndToolSafety.feature` | `S4-003`, `S4-008`, `S4-009`, `S4-011`, `S4-014` |
| Retrieved content cannot alter prompts, permissions, visibility, budgets, or policy. | `ProvenanceAndToolSafety.feature` — Ignore instructions embedded in retrieved content | `S4-006`–`S4-009`, `S4-014` |
| Research Bot tools exclude proposal, approval, reservation, order, and broker operations. | `ProvenanceAndToolSafety.feature` — Reject a forbidden Research tool (all examples) | `S4-002`, `S4-007`, `S4-014` |
| Report completion can trigger subscribed Trading Bots without duplicate runs. | `NotificationsAndRecovery.feature` — Trigger subscribed Trading Bots without duplicate runs | `S4-010`–`S4-014` |
| Every subscriber receives a durable completion or failure notification. | `NotificationsAndRecovery.feature` — Notify every subscriber of Report completion; Notify every subscriber of Research failure | `S4-003`, `S4-004`, `S4-010`, `S4-011`, `S4-014` |
| Requests are bounded, authorized, normalized, and subscribed durably. | `RequestAuthorization.feature` — Accept a bounded shared Research request; Reject an invalid Research request (all examples) | `S4-002`–`S4-005`, `S4-014` |
| Fixture-backed approved sources and a scripted model provide deterministic execution. | `ProvenanceAndToolSafety.feature` — Preserve provenance for fixture-backed evidence; `SharedReportJourney.feature` — Share one Research Report between two Trading Bots | `S4-006`–`S4-008`, `S4-014` |
| Research execution enforces deterministic budgets and safe terminal outcomes. | `ProvenanceAndToolSafety.feature` — Stop when a Research run budget is exhausted (all examples) | `S4-002`, `S4-007`, `S4-008`, `S4-011`, `S4-014` |
| Exact report versions can be listed and fetched with freshness metadata. | `PublicationAndVisibility.feature` — Publish a refresh as a new version; `SharedReportJourney.feature` — Consume an exact Report version through Trading Bot tools | `S4-004`, `S4-009`, `S4-012`, `S4-014` |
| Interrupted work and shutdown recover without duplicate publication or notification. | `NotificationsAndRecovery.feature` — Recover an interrupted Research run after restart; Shut down Research work gracefully | `S4-011`, `S4-013`, `S4-014` |
| Two Bots share one fixture-backed report while private and refreshed reports remain isolated and versioned. | All scenarios in `SharedReportJourney.feature` | `S4-005`–`S4-014` |
| Trading Bots consume Research only through authorized report tools. | `SharedReportJourney.feature` — Consume an exact Report version through Trading Bot tools | `S4-012`, `S4-014` |

All identities, timestamps, questions, model responses, budgets, sources, and tool results are deterministic synthetic inputs. Fixture sources are repository data. No scenario contacts a real model, public web service, market-data provider, broker, credential store, network, or wall clock.
