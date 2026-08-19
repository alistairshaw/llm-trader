@stage2 @acceptance @persistence @cross-platform
Feature: Immutable portfolio decision snapshots
  A Trading Bot receives a reproducible point-in-time record of reconciled portfolio state.

  Scenario: Produce a stable hash for equivalent decision state
    Given equivalent reconciled portfolio state is supplied in different collection orders
    When a Decision Snapshot is created from each input
    Then their canonical UTF-8 content should be byte-identical
    And their lowercase SHA-256 content hashes should be equal

  Scenario: Preserve a published Decision Snapshot
    Given a published Decision Snapshot for a reconciled Portfolio and its assigned Trading Bot
    When a material Portfolio value changes
    Then the published Decision Snapshot should remain unchanged
    And a new Decision Snapshot should have different canonical content and content hash

  Scenario: Reload an exact Decision Snapshot after restart
    Given a Decision Snapshot contains cash, buying power, reserved capital, positions, risk utilization, cash flows, and freshness
    When the snapshot is committed and the application restarts
    Then the snapshot should retain its exact content, ownership links, reconciliation state, timestamps, schema version, and hash
