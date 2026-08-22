# Stage 7 WPF Acceptance Traceability

These Windows-only specifications are staged before the UI harness exists. `S7-015` creates `Trading.UI.Wpf.AcceptanceTests`, pins and locks Reqnroll/FlaUI dependencies, generates synchronized sources, and makes these features discoverable. `S7-017` removes `@ignore` and supplies Automation-ID/UIA3 bindings. Until then, repository validation checks their deterministic Gherkin structure and selector-independent language.

| Stage 7 criterion | Scenario(s) | Implementing task(s) |
| --- | --- | --- |
| WPF scenarios pass on Windows. | Every staged WPF scenario | `S7-015`, `S7-017`, `S7-018` |
| Create, configure, pause, resume, and inspect a Trading Bot. | Create configure pause resume and inspect a Trading Bot | `S7-006`, `S7-017` |
| Assign an eligible Portfolio. | Assign an eligible Portfolio to a Trading Bot | `S7-006`, `S7-017` |
| Trigger a Bot Run and observe status and outcome. | Trigger and observe a Bot Run | `S7-008`, `S7-017` |
| Request and read a Research Report. | Request and read a Research Report | `S7-009`, `S7-017` |
| Inspect Proposal rationale, evidence, guardrails, and freshness. | Inspect Proposal evidence and freshness | `S7-010`, `S7-017` |
| Approve or reject a Proposal as an authorized user. | Approve a proposal from the proposal queue; Reject a proposal from the proposal queue | `S7-010`, `S7-017` |
| Paper Orders and Fills appear without restart. | Observe paper Orders and Fills without restarting | `S7-011`, `S7-013`, `S7-017` |
| Research-only, human-approval, paper, and live modes are distinct. | Distinguish execution modes | `S7-005`, `S7-017` |
| Stale data, failed reconciliation, disconnected brokers, and failed runs are prominent. | Show a prominent operational warning | `S7-007`, `S7-008`, `S7-011`, `S7-017` |
| Kill switches are accessible, authorized, audited, and confirmed. | Activate an authorized kill switch with confirmation | `S7-003`, `S7-012`, `S7-017` |
| Critical controls have stable Automation IDs, names, roles, and state. | Expose critical Bot controls to UI Automation; all critical journeys | `S7-005`, `S7-015`, `S7-017` |
| View models are tested without WPF. | Presentation behavior exercised by the corresponding view journeys | `S7-005`–`S7-013` |
| FlaUI journeys avoid coordinate selectors. | Every staged WPF scenario | `S7-015`, `S7-017` |
| WPF shutdown cleanly stops the Generic Host. | Close WPF while work is active | `S7-004`, `S7-017` |
