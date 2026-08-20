using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Proposals;

namespace Trading.Core.Tests.Proposals;

[Category("ProposalOrReservationAggregates")]
[Category("ProposalGovernance")]
public sealed class ProposalReservationAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void ProposalPinsAllAuthorityReferencesAndContentIsImmutable()
    {
        var reports = new List<ResearchReportId> { ResearchReportId.New() };
        var proposal = NewProposal(evidence: reports);
        reports.Clear();
        Assert.Multiple(() =>
        {
            Assert.That(proposal.TradingBotId, Is.Not.Null);
            Assert.That(proposal.BotRunId, Is.Not.Null);
            Assert.That(proposal.PortfolioId, Is.Not.Null);
            Assert.That(proposal.ConfigurationVersionId, Is.Not.Null);
            Assert.That(proposal.PortfolioSnapshotId, Is.Not.Null);
            Assert.That(proposal.EvidenceReportIds, Has.Count.EqualTo(1));
            Assert.That(typeof(TradeProposal).GetProperty(nameof(TradeProposal.Rationale))!.SetMethod, Is.Null);
            Assert.That(typeof(TradeProposal).GetProperty(nameof(TradeProposal.RequestedAction))!.SetMethod, Is.Null);
        });
    }

    [Test]
    public void BothProposalFormsAreExplicit()
    {
        var direct = NewProposal();
        var allocation = NewProposal(new TargetAllocationAction(new Percentage(25)));
        Assert.Multiple(() =>
        {
            Assert.That(direct.ProposalType, Is.EqualTo(ProposalType.DirectTrade));
            Assert.That(direct.RequestedAction, Is.TypeOf<DirectTradeAction>());
            Assert.That(allocation.ProposalType, Is.EqualTo(ProposalType.TargetAllocation));
            Assert.That(allocation.RequestedAction, Is.TypeOf<TargetAllocationAction>());
        });
    }

    [Test]
    public void ExactEvidenceAndContentVersionAreDefensivelyPinned()
    {
        var report = new ReportEvidenceReference(ResearchReportId.New(), "series", 7, "report-hash");
        var reports = new List<ReportEvidenceReference> { report };
        var hypothesis = new HypothesisEvidenceReference(HypothesisVersionId.New(), "hypothesis-hash");
        var content = new ProposalContentVersion(4, "proposal-hash");
        var proposal = new TradeProposal(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(), PortfolioId.New(),
            TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), InstrumentId.New(),
            new DirectTradeAction(TradeSide.Buy, new Quantity(10, "shares"), ProposedOrderType.Limit,
                new Price(25, Currency.USD), ProposedTimeInForce.Day), "rationale", content, hypothesis, reports,
            Now, Now.AddHours(1));
        reports.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(proposal.ContentVersion, Is.SameAs(content));
            Assert.That(proposal.ReportEvidence, Is.EqualTo(new[] { report }));
            Assert.That(proposal.HypothesisEvidence, Is.SameAs(hypothesis));
            Assert.That(((DirectTradeAction)proposal.RequestedAction).SchemaVersion, Is.EqualTo(1));
        });
    }

    [Test]
    public void ApprovalPinsExactImmutableContentAndFreshState()
    {
        var proposal = ExactProposal();
        proposal.StartValidation(Now.AddMinutes(1));
        proposal.RequireHumanApproval(Now.AddMinutes(2));
        var state = new FreshStateReference(proposal.PortfolioSnapshotId, Now.AddMinutes(2), "fresh-state-hash");
        var approval = proposal.Approve(ProposalApprovalId.New(), new DecisionActor(ApprovalActorType.User, "operator-1"),
            "reviewed", Now.AddMinutes(3), proposal.ContentVersion, state);

        Assert.Multiple(() =>
        {
            Assert.That(approval.ReviewedContentVersion, Is.EqualTo(proposal.ContentVersion));
            Assert.That(approval.ReviewedState, Is.EqualTo(state));
            Assert.That(approval.Actor, Is.EqualTo(new DecisionActor(ApprovalActorType.User, "operator-1")));
        });
    }

    [Test]
    public void ApprovalRequiresExactVersionAndReviewedSnapshotAndCannotBypassHumanReview()
    {
        var proposal = NewProposal();
        proposal.StartValidation(Now.AddMinutes(1));
        proposal.RequireHumanApproval(Now.AddMinutes(2));
        Assert.Multiple(() =>
        {
            Assert.That(() => proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.AuthorizedPolicy, "policy", null,
                Now.AddMinutes(3), proposal.Version, proposal.PortfolioSnapshotId), Throws.InvalidOperationException);
            Assert.That(() => proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.User, "user-1", null,
                Now.AddMinutes(3), proposal.Version - 1, proposal.PortfolioSnapshotId), Throws.InvalidOperationException);
            Assert.That(() => proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.User, "user-1", null,
                Now.AddMinutes(3), proposal.Version, PortfolioDecisionSnapshotId.New()), Throws.InvalidOperationException);
        });
        var reviewedVersion = proposal.Version;
        var decision = proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.User, "user-1", "reviewed",
            Now.AddMinutes(3), reviewedVersion, proposal.PortfolioSnapshotId);
        Assert.Multiple(() =>
        {
            Assert.That(proposal.Status, Is.EqualTo(ProposalStatus.Approved));
            Assert.That(decision.ProposalVersion, Is.EqualTo(reviewedVersion));
            Assert.That(decision.StateSnapshotId, Is.EqualTo(proposal.PortfolioSnapshotId));
        });
    }

    [Test]
    public void ExpiredProposalCannotBeApproved()
    {
        var proposal = NewProposal();
        proposal.StartValidation(Now.AddMinutes(1));
        Assert.That(() => proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.AuthorizedPolicy, "policy", null,
            proposal.ValidUntil, proposal.Version, proposal.PortfolioSnapshotId), Throws.InvalidOperationException);
    }

    [Test]
    public void EvaluationAndDecisionHistoryAreAppendOnlyImmutableFacts()
    {
        var rules = new List<GuardrailRuleResult> { new("max-position", GuardrailOutcome.Passed, "within limit") };
        var proposal = NewProposal(); proposal.StartValidation(Now.AddMinutes(1));
        var evaluation = proposal.RecordEvaluation(GuardrailEvaluationId.New(), "portfolio", "policy-v1",
            GuardrailOutcome.Passed, rules, Now.AddMinutes(2), proposal.PortfolioSnapshotId);
        rules.Clear();
        proposal.Reject(ProposalApprovalId.New(), ApprovalActorType.User, "user-1", "declined", Now.AddMinutes(3),
            proposal.Version, proposal.PortfolioSnapshotId);
        Assert.Multiple(() =>
        {
            Assert.That(evaluation.RuleResults, Has.Count.EqualTo(1));
            Assert.That(typeof(GuardrailEvaluation).GetProperties().All(property => property.SetMethod is null), Is.True);
            Assert.That(typeof(ProposalApproval).GetProperties().All(property => property.SetMethod is null), Is.True);
            Assert.That(proposal.GuardrailEvaluations, Has.Count.EqualTo(1));
            Assert.That(proposal.ApprovalHistory, Has.Count.EqualTo(1));
        });
    }

    [TestCase(ProposalStatus.Recorded, ProposalStatus.Validating, true)]
    [TestCase(ProposalStatus.Validating, ProposalStatus.AwaitingHumanApproval, true)]
    [TestCase(ProposalStatus.AwaitingHumanApproval, ProposalStatus.Approved, true)]
    [TestCase(ProposalStatus.Approved, ProposalStatus.ConvertedToOrder, true)]
    [TestCase(ProposalStatus.Recorded, ProposalStatus.ConvertedToOrder, false)]
    [TestCase(ProposalStatus.Rejected, ProposalStatus.Approved, false)]
    [TestCase(ProposalStatus.Cancelled, ProposalStatus.Validating, false)]
    public void ProposalTransitionsAreTableDriven(ProposalStatus from, ProposalStatus to, bool allowed)
    {
        var proposal = ProposalIn(from);
        void Act() => TransitionProposal(proposal, to);
        if (allowed) Assert.That(Act, Throws.Nothing); else Assert.That(Act, Throws.InvalidOperationException);
    }

    [TestCaseSource(nameof(AllProposalTransitions))]
    public void EveryProposalStatePairHasAnExplicitTransitionOutcome(ProposalStatus from, ProposalStatus to, bool allowed)
    {
        var proposal = ProposalIn(from);
        void Act() => TransitionProposal(proposal, to);
        if (allowed) Assert.That(Act, Throws.Nothing); else Assert.That(Act, Throws.InvalidOperationException);
    }

    [TestCase(CapitalReservationStatus.Active, CapitalReservationStatus.Consumed, true)]
    [TestCase(CapitalReservationStatus.Active, CapitalReservationStatus.Released, true)]
    [TestCase(CapitalReservationStatus.Active, CapitalReservationStatus.Expired, true)]
    [TestCase(CapitalReservationStatus.Consumed, CapitalReservationStatus.Released, false)]
    [TestCase(CapitalReservationStatus.Released, CapitalReservationStatus.Consumed, false)]
    [TestCase(CapitalReservationStatus.Expired, CapitalReservationStatus.Consumed, false)]
    public void ReservationTransitionsAreTableDriven(CapitalReservationStatus from, CapitalReservationStatus to, bool allowed)
    {
        var reservation = ReservationIn(from);
        void Act() => TransitionReservation(reservation, to);
        if (allowed) Assert.That(Act, Throws.Nothing); else Assert.That(Act, Throws.InvalidOperationException);
    }

    [TestCaseSource(nameof(AllReservationTransitions))]
    public void EveryReservationStatePairHasAnExplicitTransitionOutcome(
        CapitalReservationStatus from, CapitalReservationStatus to, bool allowed)
    {
        var reservation = ReservationIn(from);
        void Act() => TransitionReservation(reservation, to);
        if (allowed) Assert.That(Act, Throws.Nothing); else Assert.That(Act, Throws.InvalidOperationException);
    }

    [Test]
    public void ReservationRequiresPositiveCurrencyExplicitAmountAndTerminalOperationsAreIdempotent()
    {
        Assert.That(() => NewReservation(new Money(0, Currency.USD)), Throws.TypeOf<ArgumentOutOfRangeException>());
        var consumed = NewReservation(new Money(100, Currency.USD));
        Assert.Multiple(() =>
        {
            Assert.That(consumed.Currency, Is.EqualTo(Currency.USD));
            Assert.That(consumed.Consume(Now.AddMinutes(1)), Is.True);
            Assert.That(consumed.Consume(Now.AddMinutes(2)), Is.False);
            Assert.That(() => consumed.Release(Now.AddMinutes(2)), Throws.InvalidOperationException);
        });
        var released = NewReservation(new Money(50, Currency.EUR));
        Assert.Multiple(() =>
        {
            Assert.That(released.Release(Now.AddMinutes(1)), Is.True);
            Assert.That(released.Release(Now.AddMinutes(2)), Is.False);
            Assert.That(() => released.Consume(Now.AddMinutes(2)), Throws.InvalidOperationException);
        });
    }

    [Test]
    public void ReservationOrderAttachmentIsIdempotentAndCannotBeChanged()
    {
        var reservation = NewReservation(new Money(10, Currency.USD));
        var order = OrderId.New();
        Assert.Multiple(() =>
        {
            Assert.That(reservation.AttachToOrder(order), Is.True);
            Assert.That(reservation.AttachToOrder(order), Is.False);
            Assert.That(() => reservation.AttachToOrder(OrderId.New()), Throws.InvalidOperationException);
        });
    }

    private static TradeProposal NewProposal(RequestedAction? action = null, IEnumerable<ResearchReportId>? evidence = null) =>
        new(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(), PortfolioId.New(),
            TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), InstrumentId.New(),
            action ?? new DirectTradeAction(TradeSide.Buy, new Quantity(10, "shares"), "Limit",
                new Price(25, Currency.USD), "Day"), "Within allocation", new ProposalContentVersion(1, "proposal-hash"),
            new HypothesisEvidenceReference(HypothesisVersionId.New(), "hypothesis-hash"),
            (evidence ?? [ResearchReportId.New()]).Select((id, index) => new ReportEvidenceReference(id, $"series-{index}", 1, $"report-hash-{index}")),
            Now, Now.AddHours(1));

    private static TradeProposal ExactProposal() =>
        new(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(), PortfolioId.New(),
            TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), InstrumentId.New(),
            new TargetAllocationAction(new Percentage(25)), "Within allocation", new ProposalContentVersion(1, "proposal-hash"),
            new HypothesisEvidenceReference(HypothesisVersionId.New(), "hypothesis-hash"),
            [new ReportEvidenceReference(ResearchReportId.New(), "series", 1, "report-hash")], Now, Now.AddHours(1));

    private static CapitalReservation NewReservation(Money amount) =>
        new(CapitalReservationId.New(), ApprovedProposal(), amount, Now, Now.AddMinutes(10));

    private static TradeProposal ApprovedProposal()
    {
        var proposal = NewProposal();
        proposal.StartValidation(Now);
        proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.AuthorizedPolicy, "policy", null, Now,
            proposal.Version, proposal.PortfolioSnapshotId);
        return proposal;
    }

    private static TradeProposal ProposalIn(ProposalStatus status)
    {
        var proposal = NewProposal();
        if (status == ProposalStatus.Recorded) return proposal;
        proposal.StartValidation(Now.AddMinutes(1));
        if (status == ProposalStatus.Validating) return proposal;
        if (status == ProposalStatus.AwaitingHumanApproval) { proposal.RequireHumanApproval(Now.AddMinutes(2)); return proposal; }
        if (status == ProposalStatus.Approved) { proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.AuthorizedPolicy, "policy", null, Now.AddMinutes(2), proposal.Version, proposal.PortfolioSnapshotId); return proposal; }
        if (status == ProposalStatus.Rejected) { proposal.Reject(ProposalApprovalId.New(), ApprovalActorType.User, "user", "no", Now.AddMinutes(2), proposal.Version, proposal.PortfolioSnapshotId); return proposal; }
        if (status == ProposalStatus.Cancelled) { proposal.Cancel(Now.AddMinutes(2)); return proposal; }
        if (status == ProposalStatus.Expired) { proposal.Expire(proposal.ValidUntil); return proposal; }
        if (status == ProposalStatus.ConvertedToOrder) { proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.AuthorizedPolicy, "policy", null, Now.AddMinutes(2), proposal.Version, proposal.PortfolioSnapshotId); proposal.ConvertToOrder(Now.AddMinutes(3)); return proposal; }
        throw new ArgumentOutOfRangeException(nameof(status));
    }

    private static void TransitionProposal(TradeProposal proposal, ProposalStatus target)
    {
        if (target == ProposalStatus.Recorded) throw new InvalidOperationException("Recorded is the initial state.");
        if (target == ProposalStatus.Validating) proposal.StartValidation(Now.AddMinutes(3));
        else if (target == ProposalStatus.AwaitingHumanApproval) proposal.RequireHumanApproval(Now.AddMinutes(3));
        else if (target == ProposalStatus.Approved) proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.User, "user", null, Now.AddMinutes(3), proposal.Version, proposal.PortfolioSnapshotId);
        else if (target == ProposalStatus.Rejected) proposal.Reject(ProposalApprovalId.New(), ApprovalActorType.User, "user", "no", Now.AddMinutes(3), proposal.Version, proposal.PortfolioSnapshotId);
        else if (target == ProposalStatus.Expired) proposal.Expire(proposal.ValidUntil);
        else if (target == ProposalStatus.Cancelled) proposal.Cancel(Now.AddMinutes(3));
        else if (target == ProposalStatus.ConvertedToOrder) proposal.ConvertToOrder(Now.AddMinutes(3));
        else throw new ArgumentOutOfRangeException(nameof(target));
    }

    private static CapitalReservation ReservationIn(CapitalReservationStatus status)
    {
        var reservation = NewReservation(new Money(10, Currency.USD));
        if (status == CapitalReservationStatus.Consumed) reservation.Consume(Now.AddMinutes(11));
        else if (status == CapitalReservationStatus.Released) reservation.Release(Now.AddMinutes(11));
        else if (status == CapitalReservationStatus.Expired) reservation.Expire(Now.AddMinutes(11));
        return reservation;
    }

    private static void TransitionReservation(CapitalReservation reservation, CapitalReservationStatus target)
    {
        if (target == CapitalReservationStatus.Active) throw new InvalidOperationException("Active is the initial state.");
        if (target == CapitalReservationStatus.Consumed) reservation.Consume(Now.AddMinutes(11));
        else if (target == CapitalReservationStatus.Released) reservation.Release(Now.AddMinutes(11));
        else if (target == CapitalReservationStatus.Expired) reservation.Expire(Now.AddMinutes(11));
        else throw new ArgumentOutOfRangeException(nameof(target));
    }

    private static IEnumerable<TestCaseData> AllProposalTransitions()
    {
        var allowed = new HashSet<(ProposalStatus, ProposalStatus)>
        {
            (ProposalStatus.Recorded, ProposalStatus.Validating), (ProposalStatus.Recorded, ProposalStatus.Expired),
            (ProposalStatus.Recorded, ProposalStatus.Cancelled),
            (ProposalStatus.Validating, ProposalStatus.AwaitingHumanApproval), (ProposalStatus.Validating, ProposalStatus.Approved),
            (ProposalStatus.Validating, ProposalStatus.Rejected), (ProposalStatus.Validating, ProposalStatus.Expired),
            (ProposalStatus.Validating, ProposalStatus.Cancelled),
            (ProposalStatus.AwaitingHumanApproval, ProposalStatus.Approved), (ProposalStatus.AwaitingHumanApproval, ProposalStatus.Rejected),
            (ProposalStatus.AwaitingHumanApproval, ProposalStatus.Expired), (ProposalStatus.AwaitingHumanApproval, ProposalStatus.Cancelled),
            (ProposalStatus.Approved, ProposalStatus.Cancelled), (ProposalStatus.Approved, ProposalStatus.ConvertedToOrder),
            (ProposalStatus.Expired, ProposalStatus.Expired), (ProposalStatus.ConvertedToOrder, ProposalStatus.ConvertedToOrder),
        };
        foreach (var from in Enum.GetValues<ProposalStatus>())
        {
            foreach (var to in Enum.GetValues<ProposalStatus>())
            {
                yield return new TestCaseData(from, to, allowed.Contains((from, to))).SetName($"Proposal_{from}_to_{to}");
            }
        }
    }

    private static IEnumerable<TestCaseData> AllReservationTransitions()
    {
        foreach (var from in Enum.GetValues<CapitalReservationStatus>())
        {
            foreach (var to in Enum.GetValues<CapitalReservationStatus>())
            {
                var allowed = from == CapitalReservationStatus.Active && to != CapitalReservationStatus.Active || from == to && from != CapitalReservationStatus.Active;
                yield return new TestCaseData(from, to, allowed).SetName($"Reservation_{from}_to_{to}");
            }
        }
    }
}
