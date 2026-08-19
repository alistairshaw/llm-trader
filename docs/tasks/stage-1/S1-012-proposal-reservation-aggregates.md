---
schema_version: 1
id: S1-012
title: Implement Proposal and Capital Reservation aggregates
stage: 1
status: done
priority: 730
type: feature
depends_on: [S1-007, S1-008]
labels: [domain, proposal, risk, capital]
created: 2026-08-19
updated: 2026-08-19
---

# S1-012: Implement Proposal and Capital Reservation Aggregates

## Objective

Implement the authority boundary between LLM-originated suggestions and deterministic financial authorization.

## Scope

- Implement `TradeProposal`, `GuardrailEvaluation`, and `ProposalApproval`.
- Implement `CapitalReservation` as an independent aggregate root.
- Support direct-trade and target-allocation proposal forms.
- Encode proposal, approval, evaluation, expiration, and reservation lifecycles.

## Out of Scope

- Actual risk-rule evaluation.
- Human authentication.
- Portfolio availability queries.
- Order conversion and persistence.

## Acceptance Criteria

- A Proposal references one bot, run, Portfolio, configuration, and snapshot.
- Recorded Proposal content cannot be edited.
- Expired Proposals cannot be approved.
- Approval binds to the exact Proposal version and reviewed snapshot.
- Guardrail Evaluations and Approval history are immutable.
- A Reservation amount is positive and currency-explicit.
- Terminal Reservations cannot become active again.
- Reservation consumption and release are idempotent.
- Allowed and forbidden transitions have table-driven tests.

## Validation

```powershell
.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=ProposalOrReservationAggregates"
```

## Completion Notes

Completed on 2026-08-19.

- Added explicit direct-trade and target-allocation request models and the `TradeProposal` aggregate lifecycle.
- Added append-only, immutable guardrail evaluations and approval/rejection history bound to the exact proposal version and reviewed portfolio snapshot.
- Enforced proposal expiration, human-approval authority, terminal-state behavior, and idempotent order conversion.
- Added the independent `CapitalReservation` aggregate with approved-proposal identity binding, positive currency-explicit amounts, immutable terminal states, idempotent consumption/release, expiry, and order attachment.
- Added positive, negative, immutability, idempotency, and table-driven transition tests in the `ProposalOrReservationAggregates` category.

Validation:

- `.\dev.ps1 build` — passed in Release: 0 warnings, 0 errors.
- `.\dev.ps1 test -Project tests/Trading.Core.Tests -Filter "Category=ProposalOrReservationAggregates"` — passed: 20 tests, 0 failed, 0 skipped. An initial invocation before rebuilding found no tests in the stale assembly; the recorded result is from the rebuilt Release assembly.
- `.\dev.ps1 test` — passed: 148 Core tests, 6 architecture tests, and 1 acceptance harness test; 47 intentionally deferred acceptance scenarios skipped.
- `.\dev.ps1 format` — passed with no findings. The sandboxed attempt could not read the Docker user configuration, so the same repository command was rerun with approved Docker configuration access.

Deviations: none. Follow-up tasks: none. ADRs: none.
