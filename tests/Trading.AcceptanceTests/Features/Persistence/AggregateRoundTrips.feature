@stage2 @acceptance @persistence @cross-platform
Feature: Persisted aggregate round trips
  Stage 2 aggregates retain their domain state across a complete application restart.

  Scenario: Reload an exact portfolio after an application restart
    Given a paper portfolio funded with 10000.125 USD
    And the portfolio holds 12.34567890 shares of a mapped instrument at an average cost of 123.45678901 USD
    When the application commits the portfolio state and restarts against the same database
    Then the reloaded portfolio should retain the same cash, position, ownership, and lifecycle state
    And every reloaded financial value should equal its committed decimal value exactly

  Scenario: Reload broker and instrument identity from persistence
    Given a paper Broker Connection with one Broker Account
    And an Instrument with one effective Broker Mapping
    When the broker and instrument aggregates are committed and reloaded
    Then their identities, environment, external references, precision, and effective interval should be unchanged

  Scenario: Reload a Trading Bot with immutable configuration history
    Given a Trading Bot has an active configuration and one superseded configuration
    When the Trading Bot is committed and reloaded
    Then its lifecycle state and active configuration identity should be unchanged
    And both configuration versions should retain their canonical content and activation history

  Scenario: Round trip strongly typed identities through persistence
    Given each Stage 2 aggregate has a deterministic strongly typed identity
    When the aggregates are committed and reloaded
    Then each identity should retain its original domain type and canonical value

  Scenario: Preserve UTC timestamp precision and ordering
    Given Stage 2 records have distinct UTC timestamps separated by one millisecond
    When the records are committed and reloaded in timestamp order
    Then every timestamp should retain millisecond precision in UTC
    And their chronological order should be unchanged
