@stage1 @acceptance @ignore
Feature: Strongly typed domain identities
  An identity for one domain concept must not be accepted as the identity
  of a different domain concept.

  Scenario Outline: Reject an unrelated identity
    Given an operation that requires a <required identity>
    When a <provided identity> is supplied
    Then the operation should reject the unrelated identity type

    Examples:
      | required identity | provided identity |
      | Trading Bot ID    | Portfolio ID      |
      | Portfolio ID      | Broker Account ID |
      | Trade Proposal ID | Order ID          |

  Scenario: Preserve the type of a domain identity
    Given a valid Trading Bot ID
    When the identity is formatted and parsed
    Then the same Trading Bot ID should be produced
    And it should not become another domain identity type
