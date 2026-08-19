@stage3 @acceptance @runtime @cross-platform @ignore
Feature: Pinned input and authorized tools
  A run sees immutable owned input and only tools allowed by its pinned policy.

  Scenario: Render deterministic pinned run input
    Given Run Alpha belongs to Bot Alpha and pins Config Alpha and Snapshot Alpha for Portfolio Alpha
    When its model input is rendered twice
    Then both inputs should be byte-identical
    And each input should name Bot Alpha, Portfolio Alpha, Config Alpha, and Snapshot Alpha

  Scenario: Return the pinned Portfolio Decision Snapshot
    Given Run Alpha for Bot Alpha pins Snapshot Alpha for Portfolio Alpha
    And Config Alpha authorizes GetPortfolioSnapshot
    When Run Alpha invokes GetPortfolioSnapshot
    Then the tool should return immutable Snapshot Alpha
    And the invocation and result should be recorded for Run Alpha

  Scenario: Reject a tool absent from the pinned Tool Policy
    Given Run Alpha for Bot Alpha pins Config Alpha without tool SubmitOrder
    When Run Alpha invokes SubmitOrder
    Then the tool call should be rejected as unauthorized
    And the rejected invocation should be recorded without changing Portfolio Alpha

  Scenario: Finish a run with a terminal summary
    Given Run Alpha for Bot Alpha pins Config Alpha that authorizes Finish
    When Run Alpha invokes Finish with summary "No action required" and no requested wake time
    Then Run Alpha should record the terminal summary and complete
    And its schedule decision should use Config Alpha's baseline schedule
