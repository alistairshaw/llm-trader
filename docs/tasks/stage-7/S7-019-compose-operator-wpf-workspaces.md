---
schema_version: 1
id: S7-019
title: Compose authorized operator workflows and every WPF workspace
stage: 7
status: ready
priority: 970
type: defect
depends_on: [S7-015, S7-016]
labels: [wpf, composition, authorization, test-profile]
created: 2026-08-22
updated: 2026-08-22
---
# S7-019: Compose Authorized Operator Workflows and Every WPF Workspace

## Objective
Make the published WPF application resolve production-backed authorized operator services and display every Stage 7 workspace under the deterministic test profile.

## Context
S7-017 inspection established that `HostBootstrap` registers neither implementation required by
`AuthorizedOperatorService`, does not register the service itself or an `OperatorPrincipal`, and therefore leaves
`App.xaml.cs` unable to resolve the operator query and command boundary. The WPF navigation composition also omits
Portfolio and Execution view-model factories. Read [Architecture](../../architecture.md),
[Stage 7](../../implementation-plan.md#9-stage-7-wpf-operator-interface),
[WPF UI Acceptance](../../test-plan.md#11-wpf-ui-acceptance-tests), and
[Local Development](../../local-development.md#5-wpf-build-and-execution-boundary).

## Scope
- Implement production `IOperatorAuthorization` and `IOperatorWorkflowPort` adapters over the existing application,
  repository, and query boundaries for every Stage 7 query and command.
- Register the authorization adapter, workflow adapter, `AuthorizedOperatorService` under each of its query and command
  interfaces, and the WPF operator principal in the Generic Host.
- Bind the operator principal to explicit configured permissions and resource scope; enforce the same authorization for
  queries and commands.
- Compose Bot, Portfolio, Bot Run, Research, Proposal, Execution/Risk, and kill-switch view models into the WPF shell.
- Implement the production Portfolio view source over `IOperatorPortfolioBrokerQueries` and compose
  `ExecutionRiskAuditViewModel` over `IOrderExecutionQueries` with an authorized execution-query principal.
- Extend the deterministic WPF profile with stable scenario fixture controls for Bot/configuration/lifecycle,
  unassigned Portfolio assignment, terminal Bot Run, Research publication, awaiting Proposal approval and rejection,
  paper Order and Fill updates, each execution mode, each operational warning, hierarchical kill-switch state, and
  active recoverable shutdown work.
- Keep fixture controls bounded to the explicit WPF test profile with no network, credential, live-broker, or live-order
  authority.
- Add production-composed tests that resolve every required service from `HostBootstrap`, execute each operator action
  through `AuthorizedOperatorService`, and prove denied permissions and out-of-scope resources remain denied.
- Update architecture, local-development, test-plan, and WPF test-profile documentation when composition or fixture
  behavior changes.

## Out of Scope
- FlaUI page objects, step bindings, selectors, and WPF journey execution.
- Network providers, broker credentials, live broker adapters, and live-money submission.
- Changes to Stage 7 Gherkin language.

## Acceptance Criteria
- The published deterministic WPF application displays all seven operational workspaces with their stable workspace
  Automation IDs and never selects placeholder content for those routes.
- Every Stage 7 operator query and command resolves through `AuthorizedOperatorService` and a production workflow port.
- Bot/configuration/lifecycle, Portfolio assignment, run, Research, Proposal decision, execution, warning, mode,
  kill-switch, and active-shutdown fixture states are selectable through bounded deterministic profile inputs.
- The profile has no network endpoint, credential setting, live broker implementation, or live-order authority.
- Production-composed tests prove successful authorized operations and stable denials for missing permissions and
  out-of-scope resources.
- Release build and formatter validation pass with zero warnings or violations.

## Validation
- `./dev.ps1 build`
- Focused production-composition and WPF view-model tests
- `./dev.ps1 publish-wpf`
- Launch the published deterministic profile and inspect every route workspace Automation ID
- `./dev.ps1 test`
- `./dev.ps1 format`

## Completion Notes
Pending implementation.
