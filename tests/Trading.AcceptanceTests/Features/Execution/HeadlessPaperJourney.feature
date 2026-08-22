@stage6 @acceptance @paper-trading @execution @accounting @recovery @cross-platform @ignore
Feature: Demonstrate the complete paper trading audit chain
  The deterministic headless host proves the first complete research-to-Fill trading slice.

  Scenario: Execute an approved paper trade through partial and final Fills
    Given the headless host uses deterministic time, identities, migrated temporary SQLite, fixture research, scripted models, and Simulated Paper Broker Alpha
    When Bot Alpha requests Report Alpha, records Proposal Alpha, receives Approval Alpha, reserves 700.00 USD, and creates Order Alpha
    And Simulated Paper Broker Alpha acknowledges Order Alpha and emits partial Fill Alpha and final Fill Beta
    Then Position Acme and the exact trade and fee ledger entries should reflect Fill Alpha and Fill Beta
    And Reservation Alpha should be consumed and Order Alpha should be Filled

  Scenario: Reconstruct the complete execution audit chain
    Given the complete headless paper journey has finished
    When the execution audit projection is queried for Order Alpha
    Then it should link Report Alpha version 1, Run Alpha, Proposal Alpha version 1, Approval Alpha, Evaluation Alpha sequence 2, Reservation Alpha, Order Alpha, Outbox Alpha, broker Order Alpha, Fill Alpha, and Fill Beta
    And every identity, UTC time, exact decimal, state transition, inbox outcome, and ledger source should be present

  Scenario: Reproduce the headless paper journey deterministically
    Given the complete headless paper journey is run twice from empty migrated databases
    When their safe summaries are compared
    Then both runs should contain identical business identities, client order ID, Fill values, Position, ledger, and audit outcomes
    And neither run should contact a public service, real model, market-data feed, or live broker

  Scenario: Keep live execution disabled in the headless demonstration
    Given Live Connection Alpha is present but disabled
    When the complete headless paper journey runs
    Then every broker operation should target Simulated Paper Broker Alpha
    And Live Connection Alpha should receive zero operations
