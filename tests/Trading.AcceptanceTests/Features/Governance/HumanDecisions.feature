@stage5 @acceptance @proposals @risk @cross-platform
Feature: Apply authorized human proposal decisions
  A human decision binds one actor to the exact proposal content and reviewed state.

  Scenario: Approve the exact proposal and reviewed state
    Given Proposal Alpha version 1 passed Evaluation Alpha sequence 1 against State Risk Alpha version 1
    And User Alice is authorized to decide proposals for Portfolio Alpha
    When User Alice approves Proposal Alpha version 1 after reviewing State Risk Alpha version 1 at 2026-08-20T14:20:00.000Z
    Then an immutable approval should record User Alice, Proposal Alpha version 1, State Risk Alpha version 1, Approved, the reason, and timestamp
    And Proposal Alpha should become approved only for that content and reviewed state

  Scenario: Record an authorized rejection
    Given Proposal Alpha version 1 passed Evaluation Alpha sequence 1 against State Risk Alpha version 1
    And User Alice is authorized to decide proposals for Portfolio Alpha
    When User Alice rejects Proposal Alpha version 1 with reason PositionTooConcentrated at 2026-08-20T14:21:00.000Z
    Then an immutable rejection should identify User Alice and the exact proposal and reviewed state
    And Proposal Alpha should not be executable

  Scenario: Reject a decision by an unauthorized actor
    Given Proposal Alpha version 1 belongs to Portfolio Alpha
    And User Mallory has no proposal-decision authority for Portfolio Alpha
    When User Mallory attempts to approve Proposal Alpha version 1 at 2026-08-20T14:22:00.000Z
    Then the decision should be rejected with reason ActorNotAuthorized
    And Proposal Alpha should have no approval by User Mallory

  Scenario: Reject approval after proposal content changes
    Given User Alice reviewed Proposal Alpha version 1 against State Risk Alpha version 1
    And Proposal Beta version 1 contains changed quantity from a later Bot Run
    When User Alice submits the reviewed approval for Proposal Beta at 2026-08-20T14:23:00.000Z
    Then the approval should be rejected with reason ProposalVersionMismatch
    And neither proposal should gain authority from the mismatched decision

  Scenario: Reject approval of an expired proposal
    Given Proposal Alpha version 1 expired at 2026-08-20T14:24:00.000Z
    And User Alice is authorized to decide proposals for Portfolio Alpha
    When User Alice attempts approval at 2026-08-20T14:24:01.000Z
    Then the approval should be rejected with reason ProposalExpired
    And Proposal Alpha should become expired without a reservation
