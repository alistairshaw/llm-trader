@stage6 @acceptance @paper-trading @execution @recovery @idempotency @cross-platform @ignore
Feature: Recover durable paper execution after restart
  Pending submission, reconciliation, broker events, and Fill accounting resume from persisted state.

  Scenario: Resume pending submission outbox work after restart
    Given Outbox Alpha is pending for Order Alpha when Host Alpha stops
    When Host Beta starts at 2026-08-21T14:10:00.000Z
    Then Host Beta should claim Outbox Alpha and submit client order ID paper-order-alpha-v1
    And exactly one simulated broker Order Alpha should exist

  Scenario: Resume unknown submission reconciliation after restart
    Given Order Alpha awaits reconciliation when Host Alpha stops
    When Host Beta starts at 2026-08-21T14:10:00.000Z
    Then Host Beta should reconcile client order ID paper-order-alpha-v1 before submission
    And the durable reconciliation outcome should determine the next action

  Scenario: Resume pending broker inbox work after restart
    Given Inbox Ack Alpha and Inbox Fill Alpha are pending when Host Alpha stops
    When Host Beta starts at 2026-08-21T14:10:00.000Z
    Then Host Beta should process Ack Alpha before Fill Alpha
    And both inbox records should reach stable terminal outcomes

  Scenario: Recover an interrupted Fill transaction
    Given Fill Alpha inbox work exists but its accounting transaction did not commit
    When Host Beta processes Fill Alpha after restart
    Then Fill Alpha should update every accounting target exactly once
    And no partial state from Host Alpha should be visible

  Scenario: Reclaim expired execution leases without stealing active work
    Given Worker Alpha holds expired Outbox Alpha and Worker Beta holds unexpired Inbox Beta
    When recovery runs at 2026-08-21T14:10:00.000Z
    Then Outbox Alpha should become claimable
    And Inbox Beta should remain owned by Worker Beta
