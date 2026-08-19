@stage2 @acceptance @persistence @cross-platform
Feature: Application-facing persistence boundaries
  Application code observes domain aggregates and immutable read projections without persistence implementation types.

  Scenario: Load domain aggregates through repositories
    Given persisted Stage 2 aggregate state
    When the application loads state through repository contracts
    Then the repositories should return domain aggregate roots
    And no repository contract should expose an EF entity, DbSet, or IQueryable

  Scenario: Query portfolio projections without tracking
    Given persisted Portfolios, Positions, ledger entries, Broker Accounts, and Decision Snapshots
    When the application queries a paged Portfolio projection
    Then the projection should contain exact domain values in deterministic order
    And the persistence change tracker should remain empty

  Scenario: Exercise repositories against SQLite
    Given an isolated SQLite database with foreign keys enabled
    When repository and transaction scenarios execute
    Then they should use the real SQLite provider
    And no scenario should use the EF in-memory provider
