@stage5 @acceptance @proposals @risk @cross-platform @ignore
Feature: Keep proposal governance outside model and broker authority
  Execution mode and architecture prevent an LLM proposal from becoming a broker operation.

  Scenario: Record a ResearchOnly proposal without execution authority
    Given Run Alpha pins Bot Alpha Config Alpha version 3 in ResearchOnly mode and Snapshot Alpha version 7
    When the scripted model records valid Proposal Alpha version 1
    Then Proposal Alpha should be retained for review with status ResearchOnly
    And it should not become approvable, reservable, or executable

  Scenario: Exclude privileged tools from the Trading Bot tool surface
    Given Run Alpha uses the pinned tool policy from Config Alpha version 3
    When the Trading Bot tool catalog is listed
    Then it should contain ProposeTrade and ProposeTargetAllocation
    And it should contain no order-submission, approval, reservation, or guardrail-management tool

  Scenario: Reject a model request for broker submission
    Given Run Alpha pins Bot Alpha Config Alpha version 3 and Portfolio Alpha
    When the scripted model requests SubmitOrder with deterministic arguments
    Then the request should be rejected with reason UnknownTool
    And no broker adapter should be invoked

  Scenario: Keep proposal processing outside the model session
    Given Run Alpha recorded Proposal Alpha version 1 and called Finish
    When deterministic proposal processing begins
    Then guardrail evaluation, human decision, and capital reservation should use application services
    And the completed model session should receive no authority to alter their results

