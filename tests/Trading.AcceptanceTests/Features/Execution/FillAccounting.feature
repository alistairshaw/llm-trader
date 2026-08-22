@stage6 @acceptance @paper-trading @execution @accounting @idempotency @concurrency @cross-platform
Feature: Apply paper Fills atomically
  Each unique Fill changes the Order, Position, ledger, and Reservation together using exact decimals.

  Scenario: Apply a partial Fill atomically
    Given acknowledged Order Alpha buys 10.00000000 Acme at a limit of 70.00000000 USD
    And Reservation Alpha holds 700.00 USD
    When Fill Alpha buys 4.00000000 Acme at 69.50000000 USD with a 1.25 USD fee
    Then Order Alpha should have 4.00000000 filled and 6.00000000 remaining
    And Position Acme, trade ledger entry Fill Alpha, fee ledger entry Fill Alpha, applied-fill marker Alpha, and Reservation Alpha should commit atomically

  Scenario: Apply the final Fill and consume the Reservation
    Given Fill Alpha partially filled Order Alpha with 4.00000000 Acme
    When Fill Beta buys the remaining 6.00000000 Acme at 69.75000000 USD with a 1.50 USD fee
    Then Order Alpha should become Filled with weighted average price 69.65000000 USD
    And Reservation Alpha should be consumed with unused capital released exactly once

  Scenario: Ignore a duplicate Fill
    Given Fill Alpha has updated Order Alpha, Position Acme, the ledger, and Reservation Alpha
    When the same broker Fill Alpha is delivered again
    Then every financial balance and filled quantity should remain unchanged
    And no duplicate trade or fee ledger source should exist

  Scenario: Roll back every state change when Fill accounting fails
    Given acknowledged Order Alpha and Reservation Alpha are eligible for Fill Alpha
    And the ledger write for Fill Alpha fails deterministically
    When Fill Alpha is applied
    Then Order Alpha, Position Acme, ledger, applied-fill marker, and Reservation Alpha should all remain unchanged
    And Inbox Fill Alpha should remain available for safe recovery

  Scenario: Reject an overfill
    Given Order Alpha has 2.00000000 units remaining
    When Fill Beta reports 3.00000000 units
    Then Fill Beta should fail with reason order_execution.overfill
    And no financial or Order state should change

  Scenario: Serialize concurrent Fills for one Order
    Given Order Alpha has 10.00000000 units remaining
    When Fill Alpha for 4.00000000 and Fill Beta for 6.00000000 are applied concurrently
    Then both unique Fills should be reflected exactly once
    And Order Alpha, Position Acme, ledger, and Reservation Alpha should equal the serial result
