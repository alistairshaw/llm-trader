@stage4 @acceptance @research @cross-platform
Feature: Equivalent Research deduplication and fresh reuse
  Deterministic policy shares safe work while preserving visibility and freshness.

  Scenario: Deduplicate equivalent concurrent shared requests
    Given Bot Alpha and Bot Beta submit equivalent shared Requests Alpha and Beta concurrently
    When the Research request service authorizes both requests
    Then exactly one Research run should be queued for their normalized research key
    And both Bots should have durable subscriptions to that run

  Scenario: Reuse a sufficiently fresh equivalent Report
    Given Report Acme version 1 is authorized for Bot Alpha and fresh until 2026-08-27T12:00:00.000Z
    When Bot Alpha submits an equivalent request at 2026-08-21T12:00:00.000Z
    Then Report Acme version 1 should satisfy the request
    And no new Research run should be queued

  Scenario: Refresh an expired equivalent Report
    Given Report Acme version 1 expired at 2026-08-20T12:00:00.000Z
    When Bot Alpha submits an equivalent request at 2026-08-21T12:00:00.000Z
    Then a new Research run should be queued for Report Acme version 2
    And Report Acme version 1 should remain available as an exact historical version

  Scenario: Do not merge requests with different private inputs
    Given Bot Alpha and Bot Beta submit otherwise equivalent BotPrivate requests with different private inputs
    When the Research request service evaluates deduplication
    Then separate Research runs should be queued
    And neither Bot should subscribe to the other Bot's request
