@stage3 @acceptance @runtime @recovery @cross-platform @ignore
Feature: Runtime recovery and headless hosting
  Runtime state survives process boundaries and controlled shutdown.

  Scenario: Recover an expired run lease after restart
    Given Run Alpha for Bot Alpha holds Lease Alpha expired at 2026-08-19T14:00:00.000Z
    And Trigger Alpha remains associated with Run Alpha
    When the headless host restarts at 2026-08-19T14:01:00.000Z
    Then Lease Alpha should be recovered exactly once
    And Bot Alpha should become eligible without duplicating completed work

  Scenario: Start configured Bots in the headless host
    Given the simulated headless host contains active Bot Alpha and paused Bot Beta
    When the headless host starts
    Then supervision should start for Bot Alpha
    And no run should start for paused Bot Beta

  Scenario: Shut down the headless host gracefully
    Given the simulated headless host supervises Bot Alpha with active Run Alpha and durable Trigger Alpha
    When graceful shutdown is requested
    Then the host should stop claiming new triggers
    And Run Alpha should checkpoint or terminate safely before shutdown completes
    And Trigger Alpha and the lease decision should remain durable

  Scenario: Reconstruct a completed run audit history
    Given completed Run Alpha belongs to Bot Alpha and Portfolio Alpha
    When its audit history is loaded
    Then it should contain Config Alpha, Snapshot Alpha, Trigger Alpha, every model response and tool invocation, the terminal result, and the schedule decision
    And the history should be ordered deterministically
