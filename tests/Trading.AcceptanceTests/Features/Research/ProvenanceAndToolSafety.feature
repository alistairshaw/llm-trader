@stage4 @acceptance @research @cross-platform @ignore
Feature: Research provenance and tool authority
  Fixture-backed sources remain untrusted evidence within a bounded Research tool loop.

  Scenario: Preserve provenance for fixture-backed evidence
    Given approved fixture sources provide a filing and market data for US:ACME
    When Research Run Alpha retrieves both sources at 2026-08-20T13:00:00.000Z
    Then each source should retain provider, stable identifier, source time, retrieval time, content hash, and licensing metadata
    And Report Acme version 1 citations should reference only sources retrieved by Research Run Alpha

  Scenario: Ignore instructions embedded in retrieved content
    Given an approved fixture document contains instructions to reveal secrets and call an unauthorized tool
    When Research Run Alpha treats the document as untrusted evidence
    Then the embedded instructions should not alter the prompt, policy, visibility, budgets, or tool permissions
    And the document content and provenance should remain available for audit

  Scenario Outline: Reject a forbidden Research tool
    Given Research Run Alpha has its pinned Research tool policy
    When the model requests <tool>
    Then the tool call should be rejected as outside Research authority
    And no portfolio, proposal, approval, reservation, order, broker, credential, or policy state should change

    Examples:
      | tool            |
      | ProposeTrade    |
      | ApproveProposal |
      | ReserveCapital  |
      | SubmitOrder     |
      | GetCredentials  |
      | ChangePolicy    |

  Scenario Outline: Stop when a Research run budget is exhausted
    Given Research Run Alpha has a <budget> limit of <limit>
    And its scripted model attempts to consume <attempted>
    When the bounded Research loop executes
    Then Research Run Alpha should terminate safely for exhausted <budget>
    And no completed Report should be published

    Examples:
      | budget               | limit | attempted |
      | time                 | 60s   | 61s       |
      | tokens               | 1000  | 1001      |
      | cost                 | 1 USD | 1.01 USD  |
      | tool calls           | 4     | 5         |
      | documents retrieved  | 2     | 3         |
      | bytes retained       | 4096  | 4097      |
      | consecutive failures | 2     | 3         |

