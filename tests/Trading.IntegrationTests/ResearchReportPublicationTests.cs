using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Research;
using Trading.Research;
using Trading.Research.Contracts;

namespace Trading.IntegrationTests;

[Category("ReportPublication")]
public sealed class ResearchReportPublicationTests
{
    [Test]
    public void CompletedAttemptAcceptsOnlyItsRetrievedCitation()
    {
        var now = new DateTimeOffset(2026, 8, 20, 22, 0, 0, TimeSpan.Zero);
        var attempt = new ResearchRunAttempt(ResearchRunAttemptId.New(), ResearchRequestId.New(),
            new ResearchVersionPins("scripted", "research", "1", "prompt-v1", "tools-v1", "1"),
            new ResearchBudget(TimeSpan.FromMinutes(2), 1000, new Money(1, Currency.USD), 5, 2, 1000, 1), now.AddMinutes(-2));
        attempt.Start(now.AddMinutes(-2)); attempt.Terminate(ResearchRunAttemptStatus.Completed,
            new ResearchUsage(TimeSpan.FromMinutes(1), 10, new Money(0, Currency.USD), 2, 1, 100, 0), ResearchResultCodes.Success, now.AddMinutes(-1));
        var retrieved = new SourceCitation("approved-fixtures", "fixture://retrieved", now.AddDays(-2), now.AddDays(-1), new string('a', 64));
        var draft = new ResearchReportDraft("{\"applicabilityLimits\":[\"US\"],\"claims\":[\"claim\"],\"conclusions\":{},\"contradictoryEvidence\":[\"none found\"],\"executiveSummary\":\"summary\",\"materialRisks\":[\"risk\"],\"methodologyAndCalculations\":\"method\",\"schemaVersion\":1,\"supportingEvidence\":[\"evidence\"],\"timeHorizons\":[\"long\"],\"uncertaintyAndMissingInformation\":[\"unknown\"]}", [retrieved], now.AddDays(-1), now.AddDays(7));
        Assert.That(new ResearchReportDraftValidator().Validate(draft, attempt, [retrieved]).IsValid, Is.True);
        var unrelated = new SourceCitation(retrieved.Provider, "fixture://unrelated", retrieved.PublishedAt, retrieved.RetrievedAt, retrieved.ContentHash);
        Assert.That(new ResearchReportDraftValidator().Validate(draft, attempt, [unrelated]).ResultCode, Is.EqualTo(ResearchResultCodes.CitationInvalid));
    }
}
