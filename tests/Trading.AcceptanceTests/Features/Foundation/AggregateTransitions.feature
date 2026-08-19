@stage1 @acceptance
Feature: Aggregate lifecycle transitions
  Material lifecycle changes must follow the domain state machines and
  preserve aggregate invariants.

  Scenario: Complete an active Bot Run
    Given an active Bot Run pinned to one configuration and one decision snapshot
    When the Bot Run completes
    Then the Bot Run should become terminal
    And its completion should be recorded

  Scenario: Reject resuming a terminal Bot Run
    Given a completed Bot Run
    When an attempt is made to resume it
    Then the transition should be rejected
    And the Bot Run should remain completed

  Scenario: Approve a valid Trade Proposal
    Given a recorded Trade Proposal for its assigned Portfolio
    And the Proposal has not expired
    When the Proposal passes validation and receives its required approval
    Then the Proposal should become approved
    And the approval should identify the exact Proposal version and reviewed snapshot

  Scenario: Reject approval of an expired Trade Proposal
    Given an expired Trade Proposal
    When approval is attempted
    Then the transition should be rejected
    And the Proposal should remain expired

  Scenario Outline: Complete an active Capital Reservation
    Given an active Capital Reservation for a positive amount with an explicit currency
    When the Reservation is <action>
    Then the Reservation should become <terminal status>

    Examples:
      | action   | terminal status |
      | consumed | consumed        |
      | released | released        |
      | expired  | expired         |

  Scenario: Reject reactivating a terminal Capital Reservation
    Given a consumed Capital Reservation
    When reactivation is attempted
    Then the transition should be rejected
    And the Reservation should remain consumed

  Scenario: Fill an acknowledged Order
    Given an acknowledged Order for 10 shares
    When a fill for 10 shares is applied
    Then the Order should become filled
    And its filled quantity should be 10 shares

  Scenario: Reject an Order fill above the ordered quantity
    Given an acknowledged Order for 10 shares
    When a fill for 11 shares is applied
    Then the fill should be rejected
    And the Order should remain acknowledged
    And its filled quantity should remain zero

  Scenario Outline: Verify complete lifecycle transition coverage
    Given the documented <aggregate> lifecycle
    When its domain transition tests are run
    Then every allowed transition should be accepted
    And every forbidden transition should be rejected without changing state

    Examples:
      | aggregate           |
      | Bot Run             |
      | Trade Proposal      |
      | Capital Reservation |
      | Order               |

  Scenario: Verify positive and negative coverage of implemented aggregate invariants
    Given the invariants implemented by each Stage 1 aggregate
    When the domain invariant tests are run
    Then every invariant should have a valid example that is accepted
    And every invariant should have an invalid example that is rejected without changing state

  Scenario Outline: Preserve an aggregate invariant
    Given <valid setup>
    When <action>
    Then <positive outcome>
    But <forbidden action> should be rejected

    Examples:
      | valid setup                                                    | action                                      | positive outcome                                           | forbidden action                                      |
      | a Trading Bot with one active configuration                   | a new configuration version is activated    | the previous configuration becomes historical and immutable | editing the historical configuration                 |
      | a recorded Proposal linked to one bot, run, Portfolio, configuration, and snapshot | the Proposal is inspected | every required identity remains linked to the Proposal       | replacing any linked identity on the recorded Proposal |
      | an active Reservation for a Proposal with no other active Reservation | the Reservation is retained           | the Proposal has one active Reservation                      | creating a second active Reservation for the Proposal |
      | an Order with no fill for broker execution EX-100             | the EX-100 fill is applied                   | the fill is recorded exactly once                            | applying broker execution EX-100 again               |
