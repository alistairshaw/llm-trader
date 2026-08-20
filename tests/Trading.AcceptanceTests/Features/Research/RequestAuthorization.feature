@stage4 @acceptance @research @cross-platform @ignore
Feature: Bounded and authorized Research requests
  The shared Research service accepts only bounded questions within the requester's authority.

  Scenario: Accept a bounded shared Research request
    Given Bot Alpha is authorized to request shared company research
    And Request Alpha asks a bounded question about US:ACME as of 2026-08-20T12:00:00.000Z
    When Bot Alpha submits Request Alpha with a seven-day freshness requirement
    Then Request Alpha should be accepted with one durable subscription for Bot Alpha
    And its normalized research key should include subject, question, cutoff, sources, visibility, and schema version

  Scenario Outline: Reject an invalid Research request
    Given Bot Alpha submits a Research request with <invalid field>
    When the Research request is validated
    Then the request should be rejected with reason <reason>
    And no Research run or subscription should be created

    Examples:
      | invalid field                    | reason                 |
      | a blank question                 | question_required      |
      | an unbounded question            | question_unbounded     |
      | an unsupported source type       | source_not_authorized  |
      | a budget above platform policy   | budget_not_authorized  |
      | an unknown requesting Bot        | requester_unauthorized |

  Scenario: Prevent visibility broadening after private input
    Given Request Private contains a private input supplied by Bot Alpha
    When Request Private asks for Shared visibility
    Then Request Private should retain BotPrivate visibility for Bot Alpha
    And no model response should be able to broaden that visibility

