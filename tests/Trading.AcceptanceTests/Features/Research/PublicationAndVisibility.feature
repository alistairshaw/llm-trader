@stage4 @acceptance @research @cross-platform
Feature: Immutable versioned Research publication
  Only valid completed drafts become reports, and deterministic authorization controls access.

  Scenario: Publish a complete immutable Report
    Given Research Run Alpha has a schema-valid draft with every required section and citation
    When the publication service publishes the draft at 2026-08-20T14:00:00.000Z
    Then Report Acme version 1 should be immutable and completed
    And it should record data cutoff, generation time, expiration, schema, generator metadata, and content hash

  Scenario: Reject mutation of a published Report
    Given Report Acme version 1 has been published
    When an update attempts to replace its findings
    Then the update should be rejected
    And Report Acme version 1 and its content hash should remain unchanged

  Scenario: Publish a refresh as a new version
    Given Report Acme version 1 has been published
    And Research Run Refresh has a valid refreshed draft
    When the publication service publishes the refresh
    Then Report Acme version 2 should link to version 1
    And both immutable versions should be retrievable by exact version

  Scenario: Enforce private Report visibility
    Given Report Private version 1 is BotPrivate for Bot Alpha
    When Bot Beta lists or fetches Report Private version 1
    Then Report Private version 1 should not be disclosed to Bot Beta
    And Bot Alpha should still be able to list and fetch that exact version with freshness metadata

  Scenario: Retain failed Research without publishing it
    Given Research Run Failed has partial sources and a draft that fails citation validation
    When Research Run Failed enters its terminal state
    Then its request, sources, draft, validation result, usage, and failure reason should be retained for audit
    And no completed Report should be published from Research Run Failed
