@stage7 @acceptance @ui @windows @wpf
Feature: Review Research and Proposals through WPF
  Operators can inspect evidence and make authorized human decisions without granting model authority.

  Scenario: Request and read a Research Report
    Given Bot Alpha is authorized to request public Research
    When I request a bounded Research Report for Bot Alpha
    Then I should be able to read the published Report version and its provenance

  Scenario: Inspect Proposal evidence and freshness
    Given Proposal Alpha is awaiting human approval
    When I review Proposal Alpha
    Then I should see its rationale, exact evidence versions, guardrail results, and data freshness

  Scenario: Approve a proposal from the proposal queue
    Given a valid proposal is awaiting my approval
    When I open the proposal queue
    And I review the proposal
    And I approve it
    Then the proposal should be shown as approved
    And its paper order should be visible

  Scenario: Reject a proposal from the proposal queue
    Given Proposal Alpha is awaiting my approval
    When I review and reject Proposal Alpha with a reason
    Then Proposal Alpha should be shown as rejected
    And no Order should be created for Proposal Alpha
