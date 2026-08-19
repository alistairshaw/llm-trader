@stage2 @acceptance @persistence @cross-platform @ignore
Feature: Exclusive portfolio ownership
  Active ownership relationships remain unambiguous at the persistence boundary.

  Scenario: Reject a second active Trading Bot for one Portfolio
    Given a Portfolio is assigned to an active Trading Bot
    When another active Trading Bot is assigned to the same Portfolio
    Then the assignment should be rejected with an ownership conflict
    And the original assignment should remain unchanged

  Scenario: Reject a second active Portfolio for one Broker Account
    Given a Broker Account owns an active Portfolio
    When another active Portfolio is associated with the same Broker Account
    Then the association should be rejected with an ownership conflict
    And the original association should remain unchanged

