@stage4 @acceptance @research @cross-platform
Feature: Share fixture-backed Research between Trading Bots
  Multiple authorized Bots consume one immutable report without sharing private data.

  Scenario: Share one Research Report between two Trading Bots
    Given Bot Alpha and Bot Beta request equivalent shared fixture-backed company analysis
    And the Research Bot uses a scripted model and approved fixture sources
    When the shared Research run completes
    Then exactly one Report Acme version 1 should be published from one Research run
    And both Bots should receive durable completion notifications
    And both Bots should be able to read the same exact Report version

  Scenario: Consume an exact Report version through Trading Bot tools
    Given Bot Alpha is authorized for Report Acme versions 1 and 2
    When Bot Alpha lists Reports and fetches Report Acme version 1
    Then the catalog should show authorized version, status, freshness, and expiration
    And the fetched immutable content should be exactly Report Acme version 1

  Scenario: Complete shared, private, and refreshed Research in the headless host
    Given the headless host has two configured Trading Bots and fixture-backed Research sources
    When both Bots share a public request, Bot Alpha submits a private request, and the public Report is refreshed
    Then one shared Report series should contain immutable versions 1 and 2
    And the BotPrivate Report should remain visible only to Bot Alpha
    And the host should preserve the requests, runs, provenance, subscriptions, notifications, and Bot triggers
