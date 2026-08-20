using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;
using Trading.Research.Contracts;

namespace Trading.Research.Tests;

[Category("RequestService")]
public sealed class ResearchRequestServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 20, 0, 0, TimeSpan.Zero);
    private readonly TradingBotId bot = TradingBotId.New();

    [Test]
    public async Task EquivalentNormalizedInputsProduceSameCompleteKey()
    {
        var store = new CapturingStore(); var service = Create(store);
        var first = await service.SubmitAsync(Command(question: " Assess   durable cash flow ", sections: ["Risks", "Evidence"]), default);
        var second = await service.SubmitAsync(Command(question: "assess durable cash flow", sections: ["evidence", "risks"]), default);
        Assert.That(second.NormalizedKey, Is.EqualTo(first.NormalizedKey));
    }

    [TestCase("methodology-v2")]
    [TestCase("schema-v2")]
    [TestCase("private")]
    public async Task ReuseSensitiveDifferenceChangesKey(string difference)
    {
        var store = new CapturingStore(); var service = Create(store);
        var baseline = await service.SubmitAsync(Command(), default);
        var changed = difference switch
        {
            "methodology-v2" => Command(methodology: difference),
            "schema-v2" => Command(schema: difference),
            _ => Command(visibility: ResearchVisibility.BotPrivate, privateHash: new string('a', 64)),
        };
        Assert.That((await service.SubmitAsync(changed, default)).NormalizedKey, Is.Not.EqualTo(baseline.NormalizedKey));
    }

    [Test]
    public async Task UnauthorizedAndInvalidRequestsNeverReachPersistence()
    {
        var store = new CapturingStore(); var service = Create(store);
        var stranger = TradingBotId.New();
        var unauthorized = Command() with { Principal = new ResearchPrincipal(stranger.ToString(), ResearchPrincipalKind.TradingBot) };
        Assert.That((await service.SubmitAsync(unauthorized, default)).Code, Is.EqualTo(ResearchRequestCodes.Unauthorized));
        Assert.That((await service.SubmitAsync(Command(question: "ticker"), default)).Code, Is.EqualTo(ResearchRequestCodes.Invalid));
        Assert.That(store.Calls, Is.Zero);
    }

    [Test]
    public async Task SourcePolicyAndPrivateVisibilityAreDeterministicallyRejected()
    {
        var store = new CapturingStore(); var service = Create(store);
        Assert.That((await service.SubmitAsync(Command() with { ApprovedSourceProviders = ["market data"] }, default)).Code,
            Is.EqualTo(ResearchRequestCodes.SourcePolicyDenied));
        Assert.That((await service.SubmitAsync(Command(privateHash: new string('b', 64)), default)).Code,
            Is.EqualTo(ResearchRequestCodes.Invalid));
        Assert.That(store.Calls, Is.Zero);
    }

    [Test]
    public async Task QueuedDecisionContainsInitialIdempotentSubscription()
    {
        var store = new CapturingStore(); var result = await Create(store).SubmitAsync(Command(), default);
        Assert.Multiple(() =>
        {
            Assert.That(result.Decision, Is.EqualTo(ResearchRequestDecision.Queued));
            Assert.That(result.Code, Is.EqualTo(ResearchRequestCodes.Queued));
            Assert.That(store.Last!.Request.Status, Is.EqualTo(ResearchRequestStatus.Queued));
            Assert.That(store.Last.Request.Subscriptions.Single().Id, Is.EqualTo(result.SubscriptionId));
        });
    }

    private static ResearchRequestService Create(CapturingStore store) => new(store, new Ids(), new Clock());
    private ResearchRequestCommand Command(string question = "assess durable cash flow", string[]? sections = null,
        string methodology = "methodology-v1", string schema = "schema-v1",
        ResearchVisibility visibility = ResearchVisibility.Shared, string? privateHash = null) =>
        new(new ResearchPrincipal(bot.ToString(), ResearchPrincipalKind.TradingBot), bot, " us:aapl ", question,
            sections ?? ["evidence", "risks"], ["regulatory filings"], Now.AddDays(-1), visibility, null,
            privateHash, TimeSpan.FromDays(7), methodology, schema,
            new ResearchBudget(TimeSpan.FromMinutes(5), 1000, new Money(1, Currency.USD), 10, 10, 10_000, 2),
            ["regulatory filings"]);

    private sealed class CapturingStore : IResearchRequestDecisionRepository
    {
        public int Calls { get; private set; }
        public AuthorizedResearchRequest? Last { get; private set; }
        public Task<ResearchRequestPersistenceDecision> DecideAsync(AuthorizedResearchRequest candidate, ResearchPrincipal principal, DateTimeOffset now, CancellationToken token)
        {
            Calls++; Last = candidate;
            return Task.FromResult<ResearchRequestPersistenceDecision>(new ResearchRequestPersistenceDecision.Queued(candidate.Request.Id, candidate.SubscriptionId));
        }
    }
    private sealed class Clock : IResearchClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Ids : IResearchIdentifierSource
    {
        public ResearchRequestId NewRequestId() => ResearchRequestId.New();
        public ResearchRunAttemptId NewAttemptId() => ResearchRunAttemptId.New();
        public ResearchReportId NewReportId() => ResearchReportId.New();
        public ResearchSubscriptionId NewSubscriptionId() => ResearchSubscriptionId.New();
    }
}
