@stage2 @acceptance @persistence @cross-platform @ignore
Feature: Concurrency and transaction integrity
  Persistence conflicts and failures leave committed financial state consistent.

  Scenario: Reject a stale aggregate write
    Given two application operations load the same version of a Portfolio
    And the first operation commits a change
    When the second operation commits its stale change
    Then the second operation should receive an application concurrency conflict
    And the first committed Portfolio state should remain unchanged

  Scenario: Roll back a failed portfolio transaction
    Given a transaction will update a Position, record its applied-fill marker, and append ledger entries
    And a deterministic failure occurs after the Position write
    When the application attempts to commit the transaction
    Then no Position change, applied-fill marker, or ledger entry from the transaction should persist

