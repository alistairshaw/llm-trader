@stage2 @acceptance @persistence @migration @cross-platform @ignore
Feature: Migration and retention safety
  The Stage 2 schema is reproducible and protects retained financial and audit history.

  Scenario: Apply the initial migration to a new database
    Given a new empty SQLite database
    When the application applies all Stage 2 migrations
    Then the Stage 2 schema and migration history should be present
    And applying the migrations again should make no schema change

  Scenario: Upgrade the empty Stage 1 database fixture
    Given the empty SQLite fixture representing the released Stage 1 schema
    When the application applies all Stage 2 migrations
    Then the Stage 2 schema and migration history should be present
    And no fixture data should be lost

  Scenario: Restrict deletion of retained financial history
    Given a Portfolio has retained Position, ledger, and Decision Snapshot history
    When deletion of a referenced Portfolio is attempted
    Then the deletion should be rejected
    And all financial and audit history should remain unchanged

