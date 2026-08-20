@stage5 @acceptance @proposals @cross-platform
Feature: Record structured trade proposals
  Trading Bot suggestions become immutable proposals bound to the exact decision context and evidence.

  Scenario: Record a schema-valid direct-trade proposal
    Given Run Alpha pins Bot Alpha, Config Alpha version 3, Portfolio Alpha, and Snapshot Alpha version 7
    And the scripted ProposeTrade call buys 10 units of Instrument Acme with Report Acme version 2 and Hypothesis Growth version 4
    When the proposal tool records Proposal Alpha version 1 at 2026-08-20T14:00:00.000Z
    Then Proposal Alpha should contain the exact run, bot, configuration, portfolio, snapshot, report, and hypothesis versions
    And Proposal Alpha should remain a proposal without approval, reservation, or broker submission

  Scenario: Record a schema-valid target-allocation proposal
    Given Run Alpha pins Bot Alpha, Config Alpha version 3, Portfolio Alpha, and Snapshot Alpha version 7
    And the scripted ProposeTargetAllocation call targets 25 percent Instrument Acme and 75 percent cash
    When the proposal tool records Allocation Proposal Alpha version 1 at 2026-08-20T14:01:00.000Z
    Then Allocation Proposal Alpha should preserve its ordered targets and exact decision context
    And deterministic portfolio construction should remain downstream of proposal recording

  Scenario Outline: Reject malformed structured proposal arguments
    Given Run Alpha pins Bot Alpha, Config Alpha version 3, Portfolio Alpha, and Snapshot Alpha version 7
    And the scripted <tool> call contains <defect>
    When the proposal tool validates the call at 2026-08-20T14:02:00.000Z
    Then the call should be rejected with reason <reason>
    And no Trade Proposal should be recorded

    Examples:
      | tool                      | defect                         | reason                    |
      | ProposeTrade              | an unknown property            | UnknownProperty           |
      | ProposeTrade              | a missing portfolioSnapshotId  | MissingRequiredProperty   |
      | ProposeTrade              | a non-positive quantity        | InvalidQuantity           |
      | ProposeTargetAllocation   | allocations totaling 110       | InvalidAllocationTotal    |

  Scenario: Reject a proposal for an unassigned Portfolio
    Given Run Alpha pins Bot Alpha, Config Alpha version 3, Portfolio Alpha, and Snapshot Alpha version 7
    And the scripted ProposeTrade call names Portfolio Beta
    When the proposal tool validates the call at 2026-08-20T14:03:00.000Z
    Then the call should be rejected with reason PortfolioNotAssigned
    And Portfolio Beta should contain no proposal from Bot Alpha

  Scenario: Preserve immutable proposal content across a revision
    Given Proposal Alpha version 1 records a buy of 10 units from Run Alpha and Snapshot Alpha version 7
    When Bot Alpha proposes a buy of 12 units from Run Beta and Snapshot Alpha version 8
    Then a distinct Proposal Beta version 1 should be recorded
    And Proposal Alpha version 1 should remain unchanged
