@stage3 @acceptance @runtime @cross-platform @ignore
Feature: Isolated multi-Bot supervision
  Different Trading Bots run concurrently without crossing ownership boundaries.

  Scenario: Run two isolated Trading Bots concurrently
    Given Bot Alpha manages Portfolio Alpha with Config Alpha and Snapshot Alpha
    And Bot Beta manages Portfolio Beta with Config Beta and Snapshot Beta
    And the global runtime capacity is two runs
    When Trigger Alpha and Trigger Beta are claimed concurrently
    Then Run Alpha should receive only Config Alpha, Snapshot Alpha, and Portfolio Alpha
    And Run Beta should receive only Config Beta, Snapshot Beta, and Portfolio Beta
    And both runs should complete independently

  Scenario: Reject cross-Bot run context access
    Given Run Alpha belongs to Bot Alpha and Run Beta belongs to Bot Beta
    When Run Alpha requests Run Beta's configuration, snapshot, artifact, or Portfolio Beta
    Then every cross-Bot request should be rejected
    And neither run context should be changed

  Scenario: Respect global runtime capacity
    Given Bot Alpha and Bot Beta each have an eligible trigger
    And the global runtime capacity is one run
    When the supervisor dispatches eligible work
    Then exactly one Bot Run should hold an active lease
    And the other Bot's trigger should remain durable and eligible
