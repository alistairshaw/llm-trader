# Stage 1 Acceptance-Criteria Traceability

Scenario names are unique within the Stage 1 Foundation feature set. The implementing task column identifies the future task expected to provide the behavior, bindings, or platform evidence; `S1-015` performs the final executable acceptance review for every scenario.

| Stage 1 acceptance criterion | Feature and scenario(s) | Future implementing task(s) |
| --- | --- | --- |
| All Stage 1 Reqnroll BDD scenarios run and pass on Windows and Linux. | `BuildAndValidation.feature` — Run the Stage 1 executable specifications on the current supported platform | `S1-004`, `S1-014`, `S1-015` |
| The solution restores and builds from a clean checkout. | `BuildAndValidation.feature` — Build the solution from a clean checkout | `S1-002`, `S1-003`, `S1-014` |
| All non-WPF production and test projects build on Windows and Linux. | `BuildAndValidation.feature` — Build cross-platform projects on the current supported platform | `S1-002`, `S1-003`, `S1-014` |
| The WPF project builds on Windows. | `BuildAndValidation.feature` — Build the desktop application on Windows (`@windows`) | `S1-002`, `S1-003`, `S1-014` |
| `Trading.Core` contains no EF Core, SQLite, WPF, broker SDK, or LLM-provider dependency. | `ArchitectureBoundaries.feature` — Keep infrastructure dependencies out of the core domain | `S1-005` |
| Invalid money, quantity, price, percentage, and currency values cannot be constructed. | `FinancialValues.feature` — Reject an invalid financial value | `S1-007` |
| Strongly typed IDs prevent interchange between unrelated domain identities. | `DomainIdentities.feature` — Reject an unrelated identity; Preserve the type of a domain identity | `S1-006` |
| Allowed and forbidden Bot Run, Trade Proposal, Capital Reservation, and Order transitions have unit tests. | `AggregateTransitions.feature` — Verify complete lifecycle transition coverage, with representative allowed and forbidden lifecycle scenarios | `S1-009`, `S1-012`, `S1-013` |
| Each implemented aggregate invariant has positive and negative unit tests. | `AggregateTransitions.feature` — Verify positive and negative coverage of implemented aggregate invariants; Preserve an aggregate invariant | `S1-009`, `S1-010`, `S1-011`, `S1-012`, `S1-013` |
| Architecture tests reject prohibited project references and Windows-only APIs in cross-platform projects. | `ArchitectureBoundaries.feature` — Reject prohibited project dependencies; Reject Windows-only APIs in cross-platform projects | `S1-005` |
| A developer can run the complete applicable test suite with one documented command. | `BuildAndValidation.feature` — Use one command for complete local validation | `S1-002`, `S1-004`, `S1-015` |

All scenarios are deterministic and use no real LLM, public web, market-data, or broker service. No scenario authorizes or submits a live-money order.
