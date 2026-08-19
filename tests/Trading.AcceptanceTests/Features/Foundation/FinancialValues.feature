@stage1 @acceptance @ignore
Feature: Safe construction of financial values
  Financial values must preserve exact decimal meaning and reject values
  that do not satisfy the rules of their type.

  Scenario Outline: Construct a valid financial value
    Given a <value type> expressed as <value>
    When the financial value is constructed
    Then construction should succeed
    And its exact decimal value and unit should be preserved

    Examples:
      | value type | value      |
      | Money      | 125.50 USD |
      | Quantity   | 10 shares  |
      | Price      | 24.75 USD  |
      | Percentage | 12.5%      |
      | Currency   | USD        |

  Scenario Outline: Reject an invalid financial value
    Given an invalid <value type> expressed as <value>
    When the financial value is constructed
    Then construction should be rejected with the <reason> reason

    Examples:
      | value type | value           | reason                       |
      | Money      | 10 with no unit | currency is required         |
      | Quantity   | zero shares     | quantity must be positive    |
      | Price      | -1 USD          | price cannot be negative     |
      | Percentage | 101%            | percentage is out of range   |
      | Currency   | US              | currency code is not defined |

  Scenario: Reject arithmetic between incompatible currencies
    Given money of 10 USD
    And money of 10 EUR
    When the values are added
    Then the operation should be rejected because their currencies differ

  Scenario: Preserve precision during financial arithmetic
    Given money of 0.10 USD
    And money of 0.20 USD
    When the values are added
    Then the exact result should be 0.30 USD
