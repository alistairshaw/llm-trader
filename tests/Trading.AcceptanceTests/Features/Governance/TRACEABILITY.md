# Stage 5 Acceptance-Criteria Traceability

All Stage 5 scenarios are active and tagged `@stage5`, `@acceptance`, and `@cross-platform`. Relevant scenarios additionally use `@proposals`, `@risk`, `@concurrency`, and `@recovery`. Scenario names are unique within Stage 5. Thin steps select explicit business use cases; the scenario-scoped driver owns production host composition, deterministic inputs, migrated temporary SQLite, application queries, and durable inspection.

Run the discoverable Stage 5 specifications with:

```powershell
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage5"
```

| Stage 5 criterion or deliverable | Feature and scenario(s) | Implementing task(s) |
| --- | --- | --- |
| All Stage 5 scenarios pass on Windows and Linux. | Every Stage 5 scenario (`@cross-platform`) | `S5-014`, `S5-015` |
| Proposal tools accept only structured schema-valid arguments. | `ProposalRecording.feature` — Record a schema-valid direct-trade proposal; Record a schema-valid target-allocation proposal; Reject malformed structured proposal arguments | `S5-002`, `S5-005`, `S5-014` |
| A proposal binds exact Bot, Run, configuration, Portfolio, snapshot, Report, and Hypothesis versions. | `ProposalRecording.feature` — Record a schema-valid direct-trade proposal; Preserve immutable proposal content across a revision | `S5-002`–`S5-005`, `S5-014` |
| A Bot cannot propose for an unassigned Portfolio. | `ProposalRecording.feature` — Reject a proposal for an unassigned Portfolio | `S5-004`, `S5-005`, `S5-014` |
| Platform, account, Portfolio, and Bot guardrails execute hierarchically and children cannot weaken parents. | `GuardrailEvaluation.feature` — Pass a proposal through every policy level in order; Stop authorization when a parent guardrail rejects; Prevent a bot policy from weakening its parent | `S5-006`, `S5-007`, `S5-014` |
| Every decision records immutable structured rule results, policy versions, and state references. | `GuardrailEvaluation.feature` — Preserve immutable evaluations during revalidation; Record structured rule failures without model judgment | `S5-003`, `S5-004`, `S5-007`, `S5-014` |
| Human decisions identify actor, exact proposal version, reviewed state, decision, reason, and time. | `HumanDecisions.feature` — Approve the exact proposal and reviewed state; Record an authorized rejection; Reject a decision by an unauthorized actor | `S5-008`, `S5-014` |
| Approval cannot authorize changed content and expired proposals cannot be approved. | `HumanDecisions.feature` — Reject approval after proposal content changes; Reject approval of an expired proposal | `S5-008`, `S5-011`, `S5-014` |
| Approved proposals receive fresh-state revalidation before an order intent. | `RevalidationAndReservations.feature` — Revalidate approved content against fresh state before reservation; Reject an approved proposal when fresh state fails | `S5-007`, `S5-011`, `S5-014` |
| Concurrent proposals cannot reserve the same available capital. | `RevalidationAndReservations.feature` — Prevent two proposals from reserving the same capital; Retry reservation without duplicating the capital claim | `S5-009`, `S5-011`, `S5-014` |
| Reservations release after rejection, cancellation, or expiration. | `RevalidationAndReservations.feature` — Release capital after a terminal proposal outcome | `S5-009`, `S5-011`, `S5-014` |
| ResearchOnly records proposals without execution authority. | `ResearchOnlyAndAuthority.feature` — Record a ResearchOnly proposal without execution authority | `S5-010`, `S5-014` |
| The LLM has no order, approval, reservation, or policy-management tool and no Stage 5 path reaches broker submission. | `ResearchOnlyAndAuthority.feature` — Exclude privileged tools from the Trading Bot tool surface; Reject a model request for broker submission; Keep proposal processing outside the model session; every `HeadlessProposalJourney.feature` scenario | `S5-005`, `S5-010`, `S5-011`, `S5-013`–`S5-015` |
| The headless host demonstrates valid and invalid proposals, structured risk, human approval, capital contention, and recovery. | Every `HeadlessProposalJourney.feature` scenario | `S5-013`, `S5-014` |

Every scenario uses deterministic scripted model inputs, injected UTC time and identifiers, fixture-backed state, migrated temporary SQLite, and simulated application boundaries. No scenario contacts an external model, public web service, market-data provider, broker, or wall clock.
