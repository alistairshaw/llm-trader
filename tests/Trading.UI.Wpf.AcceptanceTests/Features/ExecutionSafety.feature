@stage7 @acceptance @ui @windows @wpf @ignore
Feature: Observe execution and operational safety through WPF
  The interface makes execution mode, risk, broker health, and emergency controls unambiguous.

  Scenario: Observe paper Orders and Fills without restarting
    Given I am viewing execution for Portfolio Alpha
    When Order Alpha receives a partial Fill and then a final Fill
    Then both Fills and the Filled Order should appear without restarting the application

  Scenario Outline: Distinguish execution modes
    Given Bot Alpha uses <mode> mode
    When I inspect Bot Alpha and its work
    Then <mode> should be exposed as the current execution mode through accessible state

    Examples:
      | mode           |
      | ResearchOnly   |
      | HumanApproval  |
      | Paper          |
      | Live           |

  Scenario Outline: Show a prominent operational warning
    Given Portfolio Alpha has <condition>
    When I inspect Portfolio Alpha operations
    Then the <warning> warning should be prominent and exposed through accessible state

    Examples:
      | condition                  | warning                 |
      | stale decision data        | StaleData               |
      | failed reconciliation      | ReconciliationFailed    |
      | a disconnected broker      | BrokerDisconnected      |
      | a failed Bot Run           | BotRunFailed            |

  @kill-switch
  Scenario: Activate an authorized kill switch with confirmation
    Given I am authorized to control the Portfolio Alpha kill switch
    When I confirm activation with a bounded reason
    Then the Portfolio Alpha kill switch should be active
    And inherited execution blocks should be visible
    And the audited outcome should identify my action

  @shutdown
  Scenario: Close WPF while work is active
    Given a Bot Run and durable paper Order work are active
    When I close the application
    Then the window should close after the Generic Host stops cleanly
    And restarting should show consistent recoverable state
