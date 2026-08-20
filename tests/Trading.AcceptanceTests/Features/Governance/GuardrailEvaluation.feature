@stage5 @acceptance @risk @cross-platform @ignore
Feature: Evaluate proposals through hierarchical guardrails
  Deterministic policy evaluates every proposal from platform through bot level without weakening parent limits.

  Scenario: Pass a proposal through every policy level in order
    Given Proposal Alpha version 1 references Snapshot Alpha version 7
    And Platform Policy 5, Account Policy 4, Portfolio Policy 8, and Bot Policy 3 authorize its measured exposure
    When Proposal Alpha is evaluated against State Risk Alpha version 1 at 2026-08-20T14:10:00.000Z
    Then evaluation stages should run in platform, account, portfolio, and bot order
    And Evaluation Alpha sequence 1 should record every policy version, measured value, limit, outcome, reason, and State Risk Alpha version 1

  Scenario: Stop authorization when a parent guardrail rejects
    Given Proposal Alpha version 1 references Snapshot Alpha version 7
    And Account Policy 4 rejects its measured exposure while lower policies would allow it
    When Proposal Alpha is evaluated against State Risk Alpha version 1 at 2026-08-20T14:11:00.000Z
    Then Evaluation Alpha sequence 1 should be rejected at the account stage
    And no lower policy outcome should authorize Proposal Alpha

  Scenario: Prevent a bot policy from weakening its parent
    Given Portfolio Policy 8 limits Instrument Acme exposure to 20 percent
    And Bot Policy 3 requests an Instrument Acme limit of 30 percent
    When the effective policy hierarchy is composed
    Then composition should be rejected with reason ChildPolicyWeakensParent
    And the effective Instrument Acme limit should not exceed 20 percent

  Scenario: Preserve immutable evaluations during revalidation
    Given Evaluation Alpha sequence 1 passed Proposal Alpha version 1 against State Risk Alpha version 1 and Policy versions 5, 4, 8, and 3
    When Proposal Alpha is revalidated against State Risk Alpha version 2 and Portfolio Policy 9 at 2026-08-20T14:12:00.000Z
    Then Evaluation Alpha sequence 2 should record the fresh state and policy versions
    And Evaluation Alpha sequence 1 should remain unchanged

  Scenario: Record structured rule failures without model judgment
    Given Proposal Alpha version 1 exceeds the deterministic cash reserve and concentration limits
    When Proposal Alpha is evaluated against State Risk Alpha version 1 at 2026-08-20T14:13:00.000Z
    Then Evaluation Alpha sequence 1 should contain separate rejected rule results for cash reserve and concentration
    And no model output should alter either rule result

