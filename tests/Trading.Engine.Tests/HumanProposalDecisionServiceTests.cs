using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Proposals;
using Trading.Engine.Proposals;

namespace Trading.Engine.Tests;

[TestFixture, Category("HumanProposalApproval")]
public sealed class HumanProposalDecisionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 16, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task AuthorizedExactReviewAppliesAndIdenticalRetryIsIdempotent()
    {
        var proposal = AwaitingApproval();
        var repository = new Repository(proposal);
        var service = Service(repository, true);
        var command = Command(proposal);

        var first = await service.DecideAsync(command, default);
        var retry = await service.DecideAsync(command, default);

        Assert.Multiple(() =>
        {
            Assert.That(first.Outcome, Is.EqualTo(HumanProposalDecisionOutcome.Applied));
            Assert.That(first.Approval!.ActorId, Is.EqualTo("operator-1"));
            Assert.That(first.Approval.ReviewedContentVersion, Is.EqualTo(proposal.ContentVersion));
            Assert.That(first.Approval.ReviewedState, Is.EqualTo(command.ReviewedState));
            Assert.That(retry.Outcome, Is.EqualTo(HumanProposalDecisionOutcome.AlreadyApplied));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        });
    }

    [TestCase(false, HumanProposalDecisionOutcome.Unauthorized, "authorization.denied")]
    [TestCase(true, HumanProposalDecisionOutcome.StaleReview, "proposal_decision.stale_review")]
    public async Task RejectsUnauthorizedOrChangedContentWithoutApplying(
        bool authorized, HumanProposalDecisionOutcome outcome, string code)
    {
        var proposal = AwaitingApproval();
        var repository = new Repository(proposal);
        var command = Command(proposal);
        if (authorized) command = command with { ReviewedContentVersion = new(2, Hash('x')) };

        var result = await Service(repository, authorized).DecideAsync(command, default);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(outcome));
            Assert.That(result.Code, Is.EqualTo(code));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.ReadCount, Is.EqualTo(authorized ? 1 : 0), "Authorization must precede disclosure.");
        });
    }

    [Test]
    public async Task RejectsExpiryStaleEvaluationAndConcurrentOrConflictingDecision()
    {
        var expired = AwaitingApproval(validUntil: Now);
        Assert.That((await Service(new Repository(expired), true).DecideAsync(Command(expired), default)).Outcome,
            Is.EqualTo(HumanProposalDecisionOutcome.Expired));

        var stale = AwaitingApproval();
        Assert.That((await Service(new Repository(stale), true).DecideAsync(Command(stale) with
        { ReviewedEvaluationHash = Hash('z') }, default)).Outcome, Is.EqualTo(HumanProposalDecisionOutcome.StaleReview));

        var concurrent = AwaitingApproval();
        var concurrentRepository = new Repository(concurrent) { Conflict = true };
        Assert.That((await Service(concurrentRepository, true).DecideAsync(Command(concurrent), default)).Outcome,
            Is.EqualTo(HumanProposalDecisionOutcome.Conflict));

        var decided = AwaitingApproval();
        await Service(new Repository(decided), true).DecideAsync(Command(decided), default);
        var conflict = await Service(new Repository(decided), true).DecideAsync(
            Command(decided) with { Decision = ApprovalDecision.Rejected, Reason = "No" }, default);
        Assert.That(conflict.Code, Is.EqualTo("proposal_decision.conflicting_terminal_decision"));
    }

    private static HumanProposalDecisionService Service(Repository repository, bool authorized) => new(
        repository, new Authorizer(authorized), new Ids(), new Clock());

    private static HumanProposalDecisionCommand Command(TradeProposal proposal)
    {
        var evaluation = proposal.GuardrailEvaluations.Single();
        return new(proposal.Id, proposal.ContentVersion, proposal.ConfigurationVersionId, evaluation.FreshState!,
            evaluation.Id, evaluation.ContentHash!, ApprovalDecision.Approved, " reviewed ",
            new DecisionActor(ApprovalActorType.User, "operator-1"), new HashSet<string> { "proposal.approve" });
    }

    private static TradeProposal AwaitingApproval(DateTimeOffset? validUntil = null)
    {
        var proposal = new TradeProposal(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(), PortfolioId.New(),
            TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), InstrumentId.New(),
            new DirectTradeAction(TradeSide.Buy, new Quantity(1, "shares"), ProposedOrderType.Market, null,
                ProposedTimeInForce.Day), "rationale", new ProposalContentVersion(1, Hash('p')), null, [],
            Now.AddHours(-1), validUntil ?? Now.AddHours(1));
        proposal.StartValidation(Now.AddMinutes(-30));
        var fresh = new FreshStateReference(proposal.PortfolioSnapshotId, Now.AddMinutes(-30), Hash('s'));
        proposal.RecordEvaluation(GuardrailEvaluationId.New(),
            [new GuardrailPolicyReference(GuardrailPolicyLevel.Platform, "platform", "v1")],
            GuardrailOutcome.Passed, [new GuardrailRuleResult("rule", GuardrailOutcome.Passed, "passed")],
            Now.AddMinutes(-30), fresh, Hash('e'), "guardrail.passed");
        proposal.CompleteValidation(GuardrailOutcome.Passed, Now.AddMinutes(-29));
        return proposal;
    }

    private static string Hash(char value) => new(value, 64);
    private sealed class Clock : IProposalGovernanceClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Ids : IProposalGovernanceIdentifierSource
    {
        public TradeProposalId NewProposalId() => TradeProposalId.New();
        public GuardrailEvaluationId NewEvaluationId() => GuardrailEvaluationId.New();
        public ProposalApprovalId NewApprovalId() => ProposalApprovalId.New();
        public CapitalReservationId NewReservationId() => CapitalReservationId.New();
    }
    private sealed class Authorizer(bool authorized) : IProposalDecisionAuthorizer
    {
        public Task<ProposalDecisionAuthorizationResult> AuthorizeAsync(ProposalDecisionAuthorizationRequest request,
            CancellationToken cancellationToken) => Task.FromResult(new ProposalDecisionAuthorizationResult(
                authorized, authorized ? "authorization.allowed" : "authorization.denied"));
    }
    private sealed class Repository(TradeProposal proposal) : ITradeProposalRepository
    {
        public int ReadCount { get; private set; }
        public int SaveCount { get; private set; }
        public bool Conflict { get; init; }
        public Task<TradeProposal?> GetAsync(TradeProposalId id, CancellationToken token)
        { ReadCount++; return Task.FromResult<TradeProposal?>(proposal); }
        public Task<ProposalRecordResult> RecordAsync(TradeProposal value, string key, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(TradeProposal value, long expectedVersion, CancellationToken token)
        { SaveCount++; return Task.FromResult<PersistenceWriteResult>(Conflict ? new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, expectedVersion + 1) : new PersistenceWriteResult.Succeeded()); }
    }
}
