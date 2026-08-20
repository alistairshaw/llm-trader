using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Policies;
using Trading.Core.Research;

namespace Trading.Core.Tests.Research;

[Category("ResearchAggregates")]
public sealed class ResearchAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [TestCase("")]
    [TestCase("   ")]
    public void ResearchQuestionMustBeNonEmpty(string question) =>
        Assert.That(() => NewRequest(question: question), Throws.ArgumentException);

    [Test]
    public void ResearchQuestionHasDeterministicBound() =>
        Assert.That(() => NewRequest(question: new string('x', 2001)), Throws.ArgumentException);

    [Test]
    public void PrivateInputsPreventVisibilityBroadeningButAllowNarrowing()
    {
        var request = NewRequest(visibility: ResearchVisibility.Restricted);
        request.RecordPrivateInputs();
        request.ChangeVisibility(ResearchVisibility.BotPrivate);
        Assert.Multiple(() =>
        {
            Assert.That(request.Visibility, Is.EqualTo(ResearchVisibility.BotPrivate));
            Assert.That(() => request.ChangeVisibility(ResearchVisibility.Shared), Throws.InvalidOperationException);
        });
    }

    [Test]
    public void OnlyAuthorizedUniqueSubscribersCanBeAttached()
    {
        var authorized = TradingBotId.New();
        var request = NewRequest(authorized: [authorized]);
        request.Subscribe(ResearchSubscriptionId.New(), authorized, Now);
        Assert.Multiple(() =>
        {
            Assert.That(request.Subscriptions, Has.Count.EqualTo(1));
            Assert.That(() => request.Subscribe(ResearchSubscriptionId.New(), authorized, Now), Throws.InvalidOperationException);
            Assert.That(() => request.Subscribe(ResearchSubscriptionId.New(), TradingBotId.New(), Now), Throws.TypeOf<UnauthorizedAccessException>());
        });
    }

    [Test]
    public void OnlyCompletedRequestLinksPublishedReport()
    {
        var request = NewRequest();
        Assert.That(() => request.Complete(ResearchReportId.New(), Now), Throws.InvalidOperationException);
        request.BeginValidation();
        request.Queue();
        request.Start(Now);
        var reportId = ResearchReportId.New();
        request.Complete(reportId, Now.AddMinutes(1));
        Assert.Multiple(() =>
        {
            Assert.That(request.Status, Is.EqualTo(ResearchRequestStatus.Completed));
            Assert.That(request.ResultReportId, Is.EqualTo(reportId));
        });
    }

    [Test]
    public void PublishedReportContentAndMetadataAreImmutableAndExplicit()
    {
        var report = NewReport();
        Assert.Multiple(() =>
        {
            Assert.That(typeof(ResearchReport).GetProperty(nameof(ResearchReport.Content))!.SetMethod, Is.Null);
            Assert.That(report.Provenance, Is.TypeOf<ReportProvenance>());
            Assert.That(report.GeneratorMetadata, Is.TypeOf<GeneratorMetadata>());
            Assert.That(report.IsFreshAt(Now.AddHours(1)), Is.True);
            Assert.That(report.IsFreshAt(Now.AddDays(2)), Is.False);
        });
    }

    [Test]
    public void RevisionMustCreateLinkedNextVersion()
    {
        var first = NewReport();
        var second = NewReport(version: 2, supersedes: first.Id);
        Assert.Multiple(() =>
        {
            Assert.That(second.VersionNumber, Is.EqualTo(2));
            Assert.That(second.SupersedesReportId, Is.EqualTo(first.Id));
            Assert.That(() => NewReport(version: 2), Throws.ArgumentException);
            Assert.That(() => NewReport(supersedes: first.Id), Throws.ArgumentException);
        });
    }

    [Test]
    public void ReportDispositionDoesNotEditPublishedContent()
    {
        var report = NewReport();
        var content = report.Content;
        report.MarkRetracted();
        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(ResearchReportStatus.Retracted));
            Assert.That(report.Content, Is.EqualTo(content));
            Assert.That(() => report.MarkExpired(), Throws.InvalidOperationException);
        });
    }

    [Test]
    public void FrozenHypothesisVersionCannotChangeAndPostFreezeChangesCreateNewVersion()
    {
        var hypothesis = NewHypothesis();
        var first = AddVersion(hypothesis);
        hypothesis.FreezeCurrent(Now.AddMinutes(1));
        var second = AddVersion(hypothesis, Now.AddMinutes(2));
        Assert.Multiple(() =>
        {
            Assert.That(first.IsFrozen, Is.True);
            Assert.That(second.VersionNumber, Is.EqualTo(2));
            Assert.That(hypothesis.CurrentVersionId, Is.EqualTo(second.Id));
            Assert.That(first.Claim, Is.EqualTo("Cash flow growth predicts returns"));
            Assert.That(typeof(HypothesisVersion).GetProperties().Where(property => property.Name != nameof(HypothesisVersion.FrozenAt)).All(property => property.SetMethod is null), Is.True);
        });
    }

    [Test]
    public void HypothesisLifecycleRequiresFreezeBeforeTestingAndTestingBeforeOutcome()
    {
        var hypothesis = NewHypothesis();
        AddVersion(hypothesis);
        Assert.That(() => hypothesis.StartTesting(), Throws.InvalidOperationException);
        hypothesis.FreezeCurrent(Now.AddMinutes(1));
        hypothesis.StartTesting();
        Assert.That(() => AddVersion(hypothesis, Now.AddMinutes(2)), Throws.InvalidOperationException);
        hypothesis.Validate();
        hypothesis.Retire();
        Assert.That(hypothesis.Status, Is.EqualTo(HypothesisStatus.Retired));
    }

    [Test]
    public void RejectedHypothesisFollowsTestingBranch()
    {
        var hypothesis = NewHypothesis();
        AddVersion(hypothesis);
        hypothesis.FreezeCurrent(Now.AddMinutes(1));
        hypothesis.StartTesting();
        hypothesis.Reject();
        Assert.That(hypothesis.Status, Is.EqualTo(HypothesisStatus.Rejected));
    }

    private static ResearchRequest NewRequest(string question = "Does cash-flow growth support a five-year thesis?",
        ResearchVisibility visibility = ResearchVisibility.Shared, IEnumerable<TradingBotId>? authorized = null) =>
        new(ResearchRequestId.New(), TradingBotId.New(), "US:ABC", question, Now.AddMinutes(-1), visibility,
            new DataFreshness(Now.AddDays(-1), Now.AddMinutes(-2), TimeSpan.FromDays(7)), "us:abc|cash-flow", Now, authorized,
            visibility == ResearchVisibility.Restricted ? "desk-a" : null);

    private static ResearchReport NewReport(int version = 1, ResearchReportId? supersedes = null) =>
        new(ResearchReportId.New(), "series-1", version, ResearchRequestId.New(), "US:ABC",
            "Does cash-flow growth support a five-year thesis?", ResearchVisibility.Shared, Now.AddHours(-1), Now,
            Now.AddDays(1), supersedes, "Structured report", "report-hash",
            new ReportProvenance([new SourceCitation("SEC", "filing-1", Now.AddDays(-1), Now.AddHours(-1), "source-hash")]),
            new GeneratorMetadata(new ModelConfiguration("provider", "model", 0m, 1000), "prompt-v1", "tools-v1", "schema-v1"));

    private static Hypothesis NewHypothesis() => new(HypothesisId.New(), "Cash flow", Now);
    private static HypothesisVersion AddVersion(Hypothesis hypothesis, DateTimeOffset? at = null) =>
        hypothesis.AddVersion(HypothesisVersionId.New(), "Cash flow growth predicts returns",
            new UniverseDefinition(["Equity"], ["NYSE"], [Currency.USD]), "FCF ttm", "Rank descending",
            "Five-year point-in-time backtest", "Excess return > 2%", "Negative excess return",
            [ResearchReportId.New()], at ?? Now);
}
