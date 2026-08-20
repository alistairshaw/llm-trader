using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;
using Trading.Research.Contracts;

namespace Trading.Research.Tests;

[Category("ReportPublication")]
public sealed class ResearchReportPublicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 20, 0, 0, TimeSpan.Zero);
    private const string Valid = """
        {"timeHorizons":["five years"],"schemaVersion":1,"executiveSummary":"Durable cash generation.","claims":["Cash flow grows."],"supportingEvidence":[{"claim":0,"citations":[0]}],"contradictoryEvidence":["Valuation is elevated."],"materialRisks":["Competition"],"uncertaintyAndMissingInformation":["Future pricing"],"methodologyAndCalculations":"Compared reported cash flow.","applicabilityLimits":["US listing"],"conclusions":{"outlook":"mixed"}}
        """;

    [Test]
    public void SchemaValidationCanonicalizationAndHashAreDeterministic()
    {
        var source = Source(); var validator = new ResearchReportDraftValidator(); var result = validator.Validate(Draft(Valid, source), CompletedAttempt(), [source]);
        var canonical = ResearchReportDraftValidator.Canonicalize(Valid);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True); Assert.That(canonical, Does.StartWith("{\"applicabilityLimits\""));
            Assert.That(ResearchReportDraftValidator.Sha256(canonical), Is.EqualTo("865cd67dc02e5b7a3b13ce38f6619748de58b6ee56cfcdfecc59f773a7393c9b"));
            Assert.That(ResearchReportDraftValidator.Sha256(ResearchReportDraftValidator.Canonicalize(canonical)), Is.EqualTo(ResearchReportDraftValidator.Sha256(canonical)));
        });
    }

    [Test]
    public void InvalidSchemaUnfinishedRunAndUnretrievedCitationAreRejectedWithStableErrors()
    {
        var source = Source(); var other = new SourceCitation(source.Provider, "fixture://other", source.PublishedAt, source.RetrievedAt, source.ContentHash); var attempt = NewAttempt();
        var result = new ResearchReportDraftValidator().Validate(Draft("{\"schemaVersion\":1}", source), attempt, [other]);
        Assert.Multiple(() => { Assert.That(result.IsValid, Is.False); Assert.That(result.ResultCode, Is.EqualTo(ResearchResultCodes.CitationInvalid)); Assert.That(result.Errors, Does.Contain("attempt.not_successfully_finished").And.Contain("schema.properties_invalid").And.Contain("citation.not_retrieved_by_attempt")); });
    }

    [Test]
    public async Task PublisherPassesCanonicalImmutableFactsToAtomicStore()
    {
        var source = Source(); var store = new Store(); var audits = new Audits(); var ids = new Ids(); var publisher = new ResearchReportPublisher(store, audits, new ResearchReportDraftValidator(), new Clock(), ids);
        var request = Request(); request.BeginValidation(); request.Queue(); request.Start(Now.AddMinutes(-2));
        var report = await publisher.PublishAsync(request, CompletedAttempt(), Draft(Valid, source), [source], null, default);
        Assert.Multiple(() => { Assert.That(report.ContentHash, Is.EqualTo("865cd67dc02e5b7a3b13ce38f6619748de58b6ee56cfcdfecc59f773a7393c9b")); Assert.That(store.Publication!.GeneratorMetadata.ReportSchemaVersion, Is.EqualTo("1")); Assert.That(store.Publication.Provenance.Sources, Is.EqualTo(new[] { source })); Assert.That(audits.Items.Single().ToolName, Is.EqualTo("ValidateReportDraft")); });
    }

    [Test]
    public void PublisherAuditsInvalidDraftWithoutPublishing()
    {
        var source = Source(); var store = new Store(); var audits = new Audits(); var request = Request(); request.BeginValidation(); request.Queue(); request.Start(Now.AddMinutes(-2));
        var publisher = new ResearchReportPublisher(store, audits, new ResearchReportDraftValidator(), new Clock(), new Ids());
        Assert.That(async () => await publisher.PublishAsync(request, NewAttempt(), Draft("{}", source), [source], null, default), Throws.TypeOf<ResearchPublicationException>());
        Assert.Multiple(() => { Assert.That(store.Publication, Is.Null); Assert.That(audits.Items.Single().Status, Is.EqualTo("Rejected")); Assert.That(audits.Items.Single().ResultJson, Does.Contain("attempt.not_successfully_finished")); });
    }

    private static ResearchReportDraft Draft(string content, SourceCitation source) => new(content, [source], Now.AddDays(-1), Now.AddDays(7));
    private static SourceCitation Source() => new("approved-fixtures", "fixture://acme/10-k", Now.AddDays(-3), Now.AddDays(-2), new string('a', 64));
    private static ResearchRunAttempt NewAttempt() => new(ResearchRunAttemptId.New(), ResearchRequestId.New(), new ResearchVersionPins("scripted", "research", "1", "prompt-v1", "tools-v1", "1"), new ResearchBudget(TimeSpan.FromMinutes(5), 1000, new Money(1, Currency.USD), 10, 5, 10000, 2), Now.AddMinutes(-3));
    private static ResearchRunAttempt CompletedAttempt() { var x = NewAttempt(); x.Start(Now.AddMinutes(-2)); x.Terminate(ResearchRunAttemptStatus.Completed, new ResearchUsage(TimeSpan.FromMinutes(1), 100, new Money(.1m, Currency.USD), 2, 1, 500, 0), ResearchResultCodes.Success, Now.AddMinutes(-1)); return x; }
    private static ResearchRequest Request() => new(ResearchRequestId.New(), TradingBotId.New(), "US:AAPL", "Five-year outlook?", Now.AddDays(-1), ResearchVisibility.Shared, new DataFreshness(Now.AddDays(-1), Now, TimeSpan.FromDays(7)), "key", Now.AddMinutes(-3));
    private sealed class Clock : IResearchClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Ids : IResearchIdentifierSource { public ResearchRequestId NewRequestId() => ResearchRequestId.New(); public ResearchRunAttemptId NewAttemptId() => ResearchRunAttemptId.New(); public ResearchReportId NewReportId() => ResearchReportId.New(); public ResearchSubscriptionId NewSubscriptionId() => ResearchSubscriptionId.New(); }
    private sealed class Store : IResearchReportRepository
    {
        public ResearchPublication? Publication { get; private set; }
        public Task<ResearchReport?> GetAsync(ResearchReportId id, CancellationToken token) => Task.FromResult<ResearchReport?>(null);
        public Task<PersistenceWriteResult> PublishAsync(ResearchReport report, ResearchRunAttemptId attemptId, CancellationToken token) => throw new NotSupportedException();
        public Task<ResearchReport> PublishCompletedAsync(ResearchPublication publication, CancellationToken token) { Publication = publication; return Task.FromResult(new ResearchReport(publication.ReportId, publication.ReportId.ToString(), 1, publication.Request.Id, publication.Request.Subject, publication.Request.Question, publication.Request.Visibility, publication.Request.AsOf, publication.GeneratedAt, publication.ExpiresAt, null, publication.CanonicalContent, publication.ContentHash, publication.Provenance, publication.GeneratorMetadata)); }
    }
    private sealed class Audits : IResearchRunAttemptRepository
    {
        public List<ResearchToolAudit> Items { get; } = [];
        public Task<ResearchRunAttempt?> GetAsync(ResearchRunAttemptId id, CancellationToken token) => Task.FromResult<ResearchRunAttempt?>(null);
        public Task<PersistenceWriteResult> SaveAsync(ResearchRunAttempt attempt, long expectedVersion, CancellationToken token) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> AppendToolAuditAsync(ResearchToolAudit audit, CancellationToken token) { Items.Add(audit); return Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); }
        public Task<IReadOnlyList<ResearchToolAudit>> GetToolAuditAsync(ResearchRunAttemptId id, CancellationToken token) => Task.FromResult<IReadOnlyList<ResearchToolAudit>>(Items);
    }
}
