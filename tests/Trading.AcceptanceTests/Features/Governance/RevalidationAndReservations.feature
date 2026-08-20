@stage5 @acceptance @risk @concurrency @cross-platform @ignore
Feature: Revalidate proposals and reserve capital atomically
  Approval becomes actionable only after fresh deterministic validation and an exclusive capital claim.

  Scenario: Revalidate approved content against fresh state before reservation
    Given User Alice approved Proposal Alpha version 1 after reviewing State Risk Alpha version 1
    And fresh State Risk Alpha version 2 still satisfies Policy versions 5, 4, 8, and 3
    When Proposal Alpha is prepared for order creation at 2026-08-20T14:30:00.000Z
    Then Evaluation Alpha sequence 2 should bind Proposal Alpha version 1 to State Risk Alpha version 2
    And Reservation Alpha should atomically reserve 1000.00000000 USD for Portfolio Alpha
    And no broker order should be submitted

  Scenario: Reject an approved proposal when fresh state fails
    Given User Alice approved Proposal Alpha version 1 after reviewing State Risk Alpha version 1
    And fresh State Risk Alpha version 2 breaches Portfolio Policy 8 cash reserve
    When Proposal Alpha is prepared for order creation at 2026-08-20T14:31:00.000Z
    Then Evaluation Alpha sequence 2 should reject Proposal Alpha against State Risk Alpha version 2
    And no capital reservation or order intent should be created

  Scenario: Prevent two proposals from reserving the same capital
    Given Portfolio Alpha has 1500.00000000 USD available in State Risk Alpha version 2
    And approved Proposal Alpha and approved Proposal Beta each require 1000.00000000 USD
    When both proposals concurrently attempt reservation at 2026-08-20T14:32:00.000Z
    Then exactly one proposal should own an active 1000.00000000 USD reservation
    And the other proposal should be rejected with reason InsufficientAvailableCapital
    And Portfolio Alpha active reservations should total 1000.00000000 USD

  Scenario Outline: Release capital after a terminal proposal outcome
    Given Reservation Alpha actively holds 1000.00000000 USD for Proposal Alpha and Portfolio Alpha
    When Proposal Alpha becomes <outcome> at <occurredAt>
    Then Reservation Alpha should become <reservationStatus> at <occurredAt>
    And 1000.00000000 USD should return to Portfolio Alpha available capital

    Examples:
      | outcome    | reservationStatus | occurredAt                   |
      | Rejected   | Released          | 2026-08-20T14:33:00.000Z     |
      | Cancelled  | Released          | 2026-08-20T14:34:00.000Z     |
      | Expired    | Expired           | 2026-08-20T14:35:00.000Z     |

  @recovery
  Scenario: Retry reservation without duplicating the capital claim
    Given Proposal Alpha version 1 already owns active Reservation Alpha for 1000.00000000 USD
    When recovery retries the same reservation command after restart at 2026-08-20T14:36:00.000Z
    Then Reservation Alpha should remain the proposal's only active reservation
    And Portfolio Alpha active reservations should remain 1000.00000000 USD

