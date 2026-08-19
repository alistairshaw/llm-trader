@stage3 @acceptance @runtime @cross-platform
Feature: Bounded scripted model execution
  Every model session terminates safely within its pinned run budget.

  Scenario Outline: Stop when a run budget is exhausted
    Given Run Alpha uses Config Alpha with a <budget> limit of <limit>
    And its scripted model attempts to consume <attempted>
    When the bounded model loop executes at 2026-08-19T14:00:00.000Z
    Then Run Alpha should terminate safely for exhausted <budget>
    And its measured time, tokens, cost, tool calls, research requests, and proposals should be recorded

    Examples:
      | budget            | limit | attempted |
      | time              | 60s   | 61s       |
      | tokens            | 1000  | 1001      |
      | cost              | 1 USD | 1.01 USD  |
      | tool calls        | 2     | 3         |
      | research requests | 0     | 1         |
      | proposals         | 0     | 1         |

  Scenario: Fail safely on a malformed model response
    Given Run Alpha uses Config Alpha and Snapshot Alpha for Portfolio Alpha
    And its scripted model returns malformed response Response Alpha
    When the bounded model loop executes
    Then Run Alpha should enter a safe failed terminal state
    And Response Alpha and the failure reason should be recorded

  Scenario: Fail safely when the model omits Finish
    Given Run Alpha uses Config Alpha and Snapshot Alpha for Portfolio Alpha
    And its scripted model exhausts its responses without invoking Finish
    When the bounded model loop executes
    Then Run Alpha should enter a safe failed terminal state for missing Finish
    And no requested schedule should replace Config Alpha's baseline schedule
