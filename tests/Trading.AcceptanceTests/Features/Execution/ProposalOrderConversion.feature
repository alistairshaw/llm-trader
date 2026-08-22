@stage6 @acceptance @paper-trading @execution @idempotency @cross-platform @ignore
Feature: Convert an approved proposal into a paper order
  Only fresh, executable proposal authority can create an Order and its durable submission work.

  Scenario: Create an Order and submission outbox atomically
    Given Proposal Alpha version 1 is approved and freshly validated against Snapshot Alpha version 8
    And Reservation Alpha holds 700.00 USD for Portfolio Alpha until 2026-08-21T15:00:00.000Z
    When Proposal Alpha is converted at 2026-08-21T14:00:00.000Z
    Then Order Alpha intent and Outbox Alpha should commit in one transaction
    And Order Alpha should bind Proposal Alpha version 1, Approval Alpha, Evaluation Alpha sequence 2, Reservation Alpha, Portfolio Alpha, and Paper Connection Alpha

  Scenario: Reject an order from a proposal without approval
    Given Proposal Alpha version 1 has no authorized approval
    When Proposal Alpha is converted at 2026-08-21T14:00:00.000Z
    Then conversion should fail with reason order_execution.approval_required
    And no Order or submission outbox should exist for Proposal Alpha

  Scenario: Reject an order from an expired proposal
    Given Proposal Alpha version 1 expired at 2026-08-21T13:59:59.000Z
    When Proposal Alpha is converted at 2026-08-21T14:00:00.000Z
    Then conversion should fail with reason order_execution.proposal_expired
    And Reservation Alpha should not be consumed

  Scenario: Reject changed or stale validated content
    Given Approval Alpha authorizes Proposal Alpha version 1
    And the latest fresh evaluation references Proposal Alpha version 2 or Snapshot Alpha version 7
    When Proposal Alpha is converted at 2026-08-21T14:00:00.000Z
    Then conversion should fail with reason order_execution.fresh_validation_required
    And no Order or submission outbox should exist for Proposal Alpha

  Scenario: Retry exact proposal conversion idempotently
    Given Proposal Alpha version 1 already created Order Alpha and Outbox Alpha
    When the exact conversion is requested again
    Then the existing Order Alpha and Outbox Alpha should be returned
    And no second Order or outbox message should be created
