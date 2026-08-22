@stage7 @acceptance @ui @windows @wpf @ignore
Feature: Operate Trading Bots through WPF
  Authorized operators manage bots and observe their bounded work through accessible controls.

  Scenario: Create configure pause resume and inspect a Trading Bot
    Given I am authorized to manage Trading Bots
    When I create Bot Alpha with a valid research-only configuration
    And I pause and resume Bot Alpha
    Then Bot Alpha should show its active configuration and current operational state

  Scenario: Assign an eligible Portfolio to a Trading Bot
    Given Portfolio Alpha has no active Trading Bot
    When I assign Portfolio Alpha to Bot Alpha
    Then Bot Alpha should show Portfolio Alpha as its assignment

  Scenario: Trigger and observe a Bot Run
    Given Bot Alpha is active with Portfolio Alpha assigned
    When I trigger a run for Bot Alpha
    Then I should observe the run status until its terminal outcome

  @accessibility
  Scenario: Expose critical Bot controls to UI Automation
    Given the Trading Bot management view is open
    Then every critical Bot control should expose a stable Automation ID
    And every critical Bot control should expose an accessible name, role, and state
