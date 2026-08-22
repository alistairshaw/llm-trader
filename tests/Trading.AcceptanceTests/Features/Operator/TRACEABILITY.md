# Stage 7 Non-UI Acceptance Traceability

These specifications are temporarily tagged `@ignore` until `S7-016` binds them through a scenario-scoped production application driver. They use only cross-platform operator contracts and are selected with:

```powershell
.\dev.ps1 test -Project tests/Trading.AcceptanceTests -Filter "TestCategory=stage7&TestCategory!=windows"
```

| Stage 7 criterion | Scenario(s) | Implementing task(s) |
| --- | --- | --- |
| Non-UI scenarios pass on Windows and Linux. | All scenarios in `OperatorSafety.feature` | `S7-016`, `S7-018` |
| Operator actions are authorized and audited. | Deny an operator command without the required authority | `S7-002`, `S7-016` |
| Kill switches are authorized, audited, hierarchical, and leave durable work recoverable. | Apply hierarchical kill switches to new work | `S7-003`, `S7-016` |
| Paper Orders and Fills update without application restart. | Deliver ordered operator updates through the application boundary | `S7-013`, `S7-016` |
| Closing the operator application stops the Generic Host cleanly without corrupting active state. | Stop the operator host cleanly with active work | `S7-004`, `S7-016` |

The complete Stage 7 criterion mapping, including Windows presentation journeys, is maintained in `tests/Trading.UI.Wpf.AcceptanceTests/Features/TRACEABILITY.md`.
