@stage1 @acceptance @infrastructure
Feature: Test infrastructure
  Stage 1 specifications must execute through the standard test command.

  Scenario: Share state within an acceptance scenario
    Given a fresh Stage 1 scenario context
    When an infrastructure marker is recorded
    Then the marker should be available to later steps
