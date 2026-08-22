@stage6 @acceptance @paper-trading @execution @idempotency @recovery @cross-platform
Feature: Submit paper orders idempotently and reconcile uncertain outcomes
  Durable work and stable client identity prevent retries from creating duplicate broker orders.

  Scenario: Submit an Order with a stable client order ID
    Given Order Alpha has pending Outbox Alpha for Paper Connection Alpha
    When the submission worker sends Order Alpha twice
    Then both attempts should use client order ID paper-order-alpha-v1
    And the simulated broker should contain exactly one broker Order Alpha

  Scenario: Retry a transient submission failure
    Given Outbox Alpha failed transiently before the broker accepted Order Alpha
    When Outbox Alpha becomes due at 2026-08-21T14:01:00.000Z
    Then the worker should retry with client order ID paper-order-alpha-v1
    And Outbox Alpha should record its bounded attempt history

  Scenario: Reconcile an unknown submission before retry
    Given submission of Order Alpha returned an unknown outcome after the broker accepted client order ID paper-order-alpha-v1
    When the submission worker resumes
    Then it should query Paper Connection Alpha for client order ID paper-order-alpha-v1 before any new submit
    And it should bind the discovered broker Order Alpha without creating another broker order

  Scenario: Defer retry while unknown reconciliation remains inconclusive
    Given Order Alpha has an unknown submission outcome
    And Paper Connection Alpha cannot yet confirm or deny client order ID paper-order-alpha-v1
    When reconciliation runs at 2026-08-21T14:02:00.000Z
    Then Order Alpha should remain awaiting reconciliation
    And no additional submission should occur

  Scenario: Submit after reconciliation proves absence
    Given Order Alpha has an unknown submission outcome
    And Paper Connection Alpha proves client order ID paper-order-alpha-v1 is absent
    When reconciliation completes at 2026-08-21T14:03:00.000Z
    Then Outbox Alpha should become eligible for retry with the same client order ID
    And its reconciliation evidence should remain durable

  Scenario: Keep paper and live broker identities distinct
    Given Paper Connection Alpha and Live Connection Alpha reference the same broker account label
    When Order Alpha is authorized for paper execution
    Then only Paper Connection Alpha should accept client order ID paper-order-alpha-v1
    And Live Connection Alpha should receive no submission or reconciliation call
