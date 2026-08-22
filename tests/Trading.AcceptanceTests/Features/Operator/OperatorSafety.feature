@stage7 @acceptance @operator @cross-platform @ignore
Feature: Authorize safe operator actions
  Operator commands use application authority and preserve durable operational safety.

  Scenario: Deny an operator command without the required authority
    Given Operator Alice is not authorized to approve proposals
    And Proposal Alpha is awaiting human approval
    When Operator Alice requests approval of Proposal Alpha
    Then the approval request should be denied
    And Proposal Alpha should remain awaiting human approval
    And the denial should be audited without exposing sensitive detail

  @kill-switch
  Scenario: Apply hierarchical kill switches to new work
    Given Bot Alpha is assigned to Portfolio Alpha on Account Alpha
    When an authorized operator activates the platform kill switch
    Then new work for Bot Alpha should be blocked by the inherited platform switch
    And the switch change should identify the operator, reason, scope, and UTC time
    And existing durable work should remain recoverable

  @updates
  Scenario: Deliver ordered operator updates through the application boundary
    Given an operator observes the paper workflow for Portfolio Alpha
    When Order Alpha receives a partial Fill and then a final Fill
    Then the operator should receive bounded ordered updates for both Fills
    And the final observed Order status should be Filled

  @hosting @shutdown
  Scenario: Stop the operator host cleanly with active work
    Given the operator host has an active Bot Run and durable pending work
    When application shutdown is requested
    Then new operator commands should stop being accepted
    And the Generic Host should stop within its bounded shutdown period
    And active state should remain consistent and recoverable
