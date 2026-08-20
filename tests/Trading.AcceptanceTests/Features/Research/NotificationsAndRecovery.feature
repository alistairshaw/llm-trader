@stage4 @acceptance @research @recovery @cross-platform @ignore
Feature: Durable Research notification and recovery
  Every subscriber learns the terminal outcome exactly once and interrupted work recovers safely.

  Scenario: Notify every subscriber of Report completion
    Given Bot Alpha and Bot Beta subscribe to Research Run Alpha
    When Report Acme version 1 is published for Research Run Alpha
    Then durable completion notifications should exist for Bot Alpha and Bot Beta
    And each notification should identify Request Alpha and Report Acme version 1

  Scenario: Notify every subscriber of Research failure
    Given Bot Alpha and Bot Beta subscribe to Research Run Failed
    When Research Run Failed terminates with reason source_unavailable
    Then durable failure notifications should exist for Bot Alpha and Bot Beta
    And no completed Report should be identified by either notification

  Scenario: Trigger subscribed Trading Bots without duplicate runs
    Given Bot Alpha and Bot Beta have durable completion notifications for Report Acme version 1
    When completion notifications are dispatched more than once
    Then each Bot should retain one report-completion trigger for Report Acme version 1
    And each Bot should start at most one follow-up Bot Run from that trigger

  Scenario: Recover an interrupted Research run after restart
    Given Research Run Alpha was interrupted while waiting for a fixture-backed source
    And its durable request, subscription, checkpoint, tool audit, and lease remain stored
    When the headless host restarts after the Research lease expires
    Then Research Run Alpha should resume or terminate according to deterministic recovery policy
    And it should not publish or notify the same terminal outcome twice

  Scenario: Shut down Research work gracefully
    Given the headless host is running Research Run Alpha and has queued Request Beta
    When graceful shutdown is requested
    Then no new Research work should start
    And active Research state and queued requests should remain recoverable after restart

