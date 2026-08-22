# Stage 6 Acceptance-Criteria Traceability

All Stage 6 scenarios are tagged `@stage6`, `@acceptance`, `@paper-trading`, `@execution`, `@cross-platform`, and the temporary `@ignore` tag. Relevant scenarios additionally use `@idempotency`, `@accounting`, `@concurrency`, and `@recovery`. `S6-015` supplies production-backed drivers and removes `@ignore`. Scenario-scoped drivers will own deterministic time and identities, migrated temporary SQLite, the simulated broker, application services, persistence inspection, and bounded diagnostics.

Run the discoverable specifications with:

```powershell
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage6"
```

| Stage 6 criterion or deliverable | Feature and scenario(s) | Implementing task(s) |
| --- | --- | --- |
| All Stage 6 scenarios pass on Windows and Linux. | Every Stage 6 scenario (`@cross-platform`) | `S6-015`, `S6-016` |
| Orders originate only from approved, unexpired, freshly validated Proposals. | `ProposalOrderConversion.feature` — Create an Order and submission outbox atomically; Reject an order from a proposal without approval; Reject an order from an expired proposal; Reject changed or stale validated content | `S6-002`, `S6-005`, `S6-007`, `S6-015` |
| Order intent and submission outbox commit atomically and exact conversion retries reuse them. | `ProposalOrderConversion.feature` — Create an Order and submission outbox atomically; Retry exact proposal conversion idempotently | `S6-004`, `S6-005`, `S6-007`, `S6-015` |
| Stable client IDs and outbox retries do not duplicate broker Orders. | `SubmissionAndReconciliation.feature` — Submit an Order with a stable client order ID; Retry a transient submission failure | `S6-003`, `S6-006`, `S6-008`, `S6-015` |
| Unknown submission outcomes reconcile before retry. | `SubmissionAndReconciliation.feature` — Reconcile an unknown submission before retry; Defer retry while unknown reconciliation remains inconclusive; Submit after reconciliation proves absence | `S6-008`, `S6-009`, `S6-015` |
| Broker acknowledgements, rejection, cancellation, and expiration advance state safely. | `BrokerOrderEvents.feature` — Acknowledge a submitted paper Order; Apply a valid terminal broker outcome | `S6-006`, `S6-010`, `S6-015` |
| Duplicate and invalid or out-of-order broker messages do not corrupt state. | `BrokerOrderEvents.feature` — Ignore a duplicate broker event; Reject an invalid broker identity; Defer a Fill that arrives before acknowledgement; Reject a terminal event after a final Fill | `S6-006`, `S6-009`, `S6-010`, `S6-015` |
| Partial and final Fills atomically update Order, Position, ledger, marker, and Reservation. | `FillAccounting.feature` — Apply a partial Fill atomically; Apply the final Fill and consume the Reservation; Roll back every state change when Fill accounting fails; Serialize concurrent Fills for one Order | `S6-010`, `S6-011`, `S6-015` |
| Duplicate Fills and overfills do not change financial state twice. | `FillAccounting.feature` — Ignore a duplicate Fill; Reject an overfill | `S6-011`, `S6-015` |
| Pending inbox, outbox, reconciliation, and Fill work resumes safely after restart. | Every `DurableRecovery.feature` scenario | `S6-006`, `S6-009`, `S6-012`, `S6-015` |
| Paper and live broker environments cannot be confused. | `SubmissionAndReconciliation.feature` — Keep paper and live broker identities distinct; `HeadlessPaperJourney.feature` — Keep live execution disabled in the headless demonstration | `S6-002`, `S6-003`, `S6-008`, `S6-014`, `S6-015` |
| Order, Fill, and audit projections expose the exact immutable chain. | `HeadlessPaperJourney.feature` — Reconstruct the complete execution audit chain | `S6-013`, `S6-015` |
| The headless host demonstrates research through final Fill deterministically. | Every `HeadlessPaperJourney.feature` scenario | `S6-014`, `S6-015` |

Every scenario uses synthetic identities and values, injected UTC time, deterministic simulated broker inputs, and isolated migrated temporary SQLite. No scenario contacts an external model, public web service, market-data provider, broker, or wall clock, and no scenario submits a live-money order.
