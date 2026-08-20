@stage5 @acceptance @proposals @risk @concurrency @cross-platform @ignore
Feature: Demonstrate proposal governance in the headless host
  The deterministic fixture-backed host shows valid, invalid, and competing proposals without broker submission.

  Scenario: Demonstrate valid and invalid scripted proposals
    Given the headless host has Bot Alpha and Bot Beta with migrated temporary SQLite and deterministic identifiers
    And fixture-backed State Risk Demo version 1 and scripted proposal responses are pinned at 2026-08-20T15:00:00.000Z
    When Bot Alpha proposes a valid direct trade and Bot Beta proposes an invalid target allocation
    Then the host should display structured accepted and rejected policy results with exact proposal, evidence, policy, and state versions
    And both durable proposal histories should be reconstructable

  Scenario: Approve one proposal and demonstrate reservation contention
    Given valid Proposal Alpha and Proposal Beta compete for Portfolio Alpha available capital
    And User Alice reviews Proposal Alpha version 1 against State Risk Demo version 1
    When User Alice approves Proposal Alpha and both proposals request reservation concurrently
    Then Proposal Alpha should own the single atomic reservation
    And Proposal Beta should report InsufficientAvailableCapital
    And the host should report zero broker submissions

  @recovery
  Scenario: Shut down and recover the proposal demonstration safely
    Given the headless host has persisted Proposal Alpha, Evaluation Alpha, Approval Alpha, and Reservation Alpha
    When the host shuts down and restarts with injected time 2026-08-20T15:05:00.000Z
    Then each immutable governance record should retain its exact identity and version
    And recovery should neither duplicate reservations nor invoke a broker

