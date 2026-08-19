@stage1 @acceptance
Feature: Cross-platform architecture boundaries
  Domain rules and cross-platform behavior must remain independent of
  infrastructure, external providers, and Windows-only technology.

  Scenario Outline: Keep infrastructure dependencies out of the core domain
    Given the core domain project
    When its production dependencies are inspected
    Then it should not depend on <prohibited dependency>

    Examples:
      | prohibited dependency |
      | EF Core               |
      | SQLite                |
      | WPF                   |
      | a broker SDK          |
      | an LLM provider       |

  Scenario: Reject prohibited project dependencies
    Given the approved solution dependency boundaries
    When production project references are inspected
    Then every project reference should follow the approved dependency direction
    And no cross-platform project should depend on the Windows desktop project
    And no production project should depend on test support

  Scenario: Reject Windows-only APIs in cross-platform projects
    Given the cross-platform production and test projects
    When their platform dependencies are inspected
    Then no cross-platform project should use a Windows-only API
