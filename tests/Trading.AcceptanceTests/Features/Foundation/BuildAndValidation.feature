@stage1 @acceptance
Feature: Stage 1 build and validation
  The solution foundation must be reproducible on supported platforms
  and have one deterministic validation entry point.

  Scenario: Build the solution from a clean checkout
    Given a clean checkout with no restored dependencies or build output
    When the developer runs the documented build command
    Then dependency restore should succeed
    And the full solution should build in Release mode with no warnings

  Scenario: Build cross-platform projects on the current supported platform
    Given a clean checkout on a supported platform
    When the cross-platform production and test projects are built
    Then every cross-platform project should build successfully

  @windows
  Scenario: Build the desktop application on Windows
    Given a clean checkout on Windows
    When the desktop application is built
    Then the Windows desktop build should succeed

  Scenario: Run the Stage 1 executable specifications on the current supported platform
    Given a clean checkout on a supported platform
    When the complete applicable test suite is run with the documented test command
    Then every applicable Stage 1 scenario should pass
    And no Stage 1 scenario should be undefined or pending

  Scenario: Use one command for complete local validation
    Given the development container is available
    When the developer runs the documented test command
    Then the complete deterministic cross-platform test suite should run
    And no real LLM, public web, live market data, or broker service should be contacted
    And no live-money order should be submitted
