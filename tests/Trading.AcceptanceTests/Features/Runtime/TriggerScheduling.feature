@stage3 @acceptance @runtime @scheduling @cross-platform @ignore
Feature: Durable triggers and scheduling
  Trigger and schedule decisions remain deterministic and durable.

  Scenario: Coalesce triggers retained during an active run
    Given Run Alpha for Bot Alpha holds an active lease
    When manual Trigger Alpha and scheduled Trigger Beta arrive for Bot Alpha
    Then both triggers should be retained durably
    And one follow-up run should coalesce Trigger Alpha and Trigger Beta

  Scenario: Accept a requested wake time inside policy bounds
    Given Config Alpha has baseline wake time 2026-08-20T14:00:00.000Z and request bounds from 2026-08-19T14:05:00.000Z through 2026-08-20T14:00:00.000Z
    When Run Alpha finishes with requested wake time 2026-08-19T16:00:00.000Z
    Then its schedule decision should accept 2026-08-19T16:00:00.000Z
    And the baseline schedule should remain enabled

  Scenario: Bound a requested wake time outside policy bounds
    Given Config Alpha has baseline wake time 2026-08-20T14:00:00.000Z and request bounds from 2026-08-19T14:05:00.000Z through 2026-08-20T14:00:00.000Z
    When Run Alpha finishes with requested wake time 2026-08-19T14:01:00.000Z
    Then its schedule decision should bound the wake time to 2026-08-19T14:05:00.000Z
    And the baseline schedule should remain enabled

  Scenario: Reject an invalid requested wake time
    Given Config Alpha has baseline wake time 2026-08-20T14:00:00.000Z and request bounds from 2026-08-19T14:05:00.000Z through 2026-08-20T14:00:00.000Z
    When Run Alpha finishes with a malformed requested wake time
    Then its schedule decision should reject the request with a recorded reason
    And schedule 2026-08-20T14:00:00.000Z from the baseline
