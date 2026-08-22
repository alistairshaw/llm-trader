@stage6 @acceptance @paper-trading @execution @idempotency @cross-platform
Feature: Process broker order events safely
  Broker acknowledgements and terminal outcomes advance Orders once without corrupting valid state.

  Scenario: Acknowledge a submitted paper Order
    Given Order Alpha was submitted as client order ID paper-order-alpha-v1
    When Broker Event Ack Alpha identifies broker Order Alpha at 2026-08-21T14:00:01.000Z
    Then Order Alpha should be acknowledged with broker Order Alpha
    And Inbox Ack Alpha should be marked applied in the same transaction

  Scenario Outline: Apply a valid terminal broker outcome
    Given acknowledged Order Alpha has no final Fill
    When Broker Event <event> reports <outcome> at 2026-08-21T14:05:00.000Z
    Then Order Alpha should become <state>
    And Reservation Alpha should be released exactly once

    Examples:
      | event          | outcome   | state     |
      | Reject Alpha   | rejection | Rejected  |
      | Cancel Alpha   | cancel    | Cancelled |
      | Expire Alpha   | expiration| Expired   |

  Scenario: Ignore a duplicate broker event
    Given Inbox Ack Alpha already applied Broker Event Ack Alpha
    When the identical Broker Event Ack Alpha is delivered again
    Then Order Alpha should remain unchanged
    And no duplicate audit transition should be recorded

  Scenario: Reject an invalid broker identity
    Given Order Alpha belongs to Paper Connection Alpha
    When Paper Connection Beta sends an event for broker Order Alpha
    Then the event should fail with reason order_execution.broker_identity_mismatch
    And Order Alpha should remain unchanged

  Scenario: Defer a Fill that arrives before acknowledgement
    Given Order Alpha is submitted without a broker Order identity
    When Fill Event Alpha arrives for broker Order Alpha before Ack Alpha
    Then Fill Event Alpha should remain pending reconciliation
    And no Order, Position, ledger, or Reservation value should change

  Scenario: Reject a terminal event after a final Fill
    Given Order Alpha is Filled by Fill Alpha and Fill Beta
    When Broker Event Cancel Alpha arrives after the final Fill
    Then the event should be retained with reason order_execution.invalid_event_order
    And Order Alpha and its accounting state should remain unchanged
