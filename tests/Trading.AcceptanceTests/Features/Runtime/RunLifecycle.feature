@stage3 @acceptance @runtime @cross-platform @ignore
Feature: Trading Bot run lifecycle
  Trading Bot runs use durable triggers and one exclusive per-Bot lease.

  Scenario: Complete a manually triggered Bot Run
    Given Bot Alpha manages Portfolio Alpha with active configuration Config Alpha
    And reconciled snapshot Snapshot Alpha is pinned for Portfolio Alpha
    And manual trigger Trigger Alpha is recorded at 2026-08-19T14:00:00.000Z
    When worker Worker One acquires the lease and runs Bot Alpha with a scripted Finish response
    Then Run Alpha should pin Config Alpha and Snapshot Alpha
    And Run Alpha should complete with its trigger, lease, result, tool invocations, and schedule decision recorded

  @scheduling
  Scenario: Start a scheduled Bot Run
    Given Bot Alpha has baseline schedule 2026-08-19T15:00:00.000Z for Config Alpha
    And reconciled snapshot Snapshot Alpha is pinned for Portfolio Alpha
    When the scheduler records Trigger Alpha at 2026-08-19T15:00:00.000Z
    Then Run Alpha should start for Bot Alpha using Config Alpha and Snapshot Alpha

  Scenario: Enforce one active lease for a Trading Bot
    Given Run Alpha for Bot Alpha holds lease Lease Alpha until 2026-08-19T14:10:00.000Z
    When worker Worker Two attempts to acquire another lease for Bot Alpha at 2026-08-19T14:05:00.000Z
    Then the lease request should be rejected
    And Run Alpha should remain the only active run for Bot Alpha
