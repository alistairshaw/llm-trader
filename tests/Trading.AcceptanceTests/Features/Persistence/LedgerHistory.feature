@stage2 @acceptance @persistence @cross-platform
Feature: Durable portfolio ledger history
  Portfolio accounting facts are append-only and idempotent by source identity.

  Scenario: Ignore a duplicate ledger source
    Given a Portfolio has a 250.125 USD deposit from source Deposit DEP-100
    When the same deposit source is appended again
    Then the ledger should contain one entry for Deposit DEP-100
    And the Portfolio financial state should change only once

  Scenario: Correct a ledger entry with a compensating entry
    Given a Portfolio ledger contains a 75.25 USD fee from source Fee FEE-100
    When the fee is corrected to 70.25 USD
    Then the original fee entry should remain unchanged
    And a compensating entry for 5 USD should reference the original entry
    And the ledger history should contain both accounting facts
