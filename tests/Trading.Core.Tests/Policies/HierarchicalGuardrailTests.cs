using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Policies;
using Trading.Core.Proposals;

namespace Trading.Core.Tests.Policies;

[Category("HierarchicalGuardrails")]
public sealed class HierarchicalGuardrailTests
{
    [Test]
    public void PoliciesComposeInFixedHierarchyOrder()
    {
        var effective = Policies().ComposeEffectivePolicies();
        Assert.That(effective.Select(x => x.Reference.Level), Is.EqualTo(new[]
        {
            GuardrailPolicyLevel.Platform, GuardrailPolicyLevel.Account,
            GuardrailPolicyLevel.Portfolio, GuardrailPolicyLevel.TradingBot,
        }));
    }

    [TestCase(1000, 2000, 1000)]
    [TestCase(1000, 500, 500)]
    public void ChildCannotWeakenMaximumPosition(decimal parent, decimal child, decimal expected)
    {
        var policies = Policies(maximums: [parent, child, 5000, 5000]);
        Assert.That(policies.ComposeEffectivePolicies()[1].MaximumPositionNotional!.Amount, Is.EqualTo(expected));
    }

    [TestCase(10, 5, 10)]
    [TestCase(10, 15, 15)]
    public void ChildCannotWeakenMinimumReserve(decimal parent, decimal child, decimal expected)
    {
        var policies = Policies(reserves: [parent, child, 0, 0]);
        Assert.That(policies.ComposeEffectivePolicies()[1].MinimumAvailableCapital!.Amount, Is.EqualTo(expected));
    }

    [Test]
    public void DisabledParentAndOpenMarketRequirementCannotBeOverridden()
    {
        var policies = Policies(enabled: [false, true, true, true], requireOpen: [true, false, false, false]);
        var effective = policies.ComposeEffectivePolicies();
        Assert.That(effective.Skip(1), Has.All.Matches<GuardrailPolicy>(x => !x.Enabled && x.RequireOpenMarket));
    }

    [Test]
    public void EligibleUniverseOnlyNarrows()
    {
        var included = InstrumentId.New();
        var removed = InstrumentId.New();
        var policies = Policies(instruments: [[included, removed], [included], null, null]);
        Assert.That(policies.ComposeEffectivePolicies()[3].EligibleInstruments, Is.EquivalentTo(new[] { included }));
    }

    [Test]
    public void IdenticalInputsProduceIdenticalOrderedResults()
    {
        var proposal = Proposal();
        var policies = Policies();
        var state = State();
        var first = HierarchicalGuardrailEvaluator.Evaluate(proposal, policies, state);
        var second = HierarchicalGuardrailEvaluator.Evaluate(proposal, policies, state);
        Assert.Multiple(() =>
        {
            Assert.That(second.Outcome, Is.EqualTo(first.Outcome));
            Assert.That(second.Code, Is.EqualTo(first.Code));
            Assert.That(second.RuleResults, Is.EqualTo(first.RuleResults));
            Assert.That(second.EvaluatedPolicies, Is.EqualTo(first.EvaluatedPolicies));
            Assert.That(first.RuleResults, Has.Count.EqualTo(44));
            Assert.That(first.RuleResults.Take(11).Select(x => x.Rule), Is.EqualTo(first.RuleResults.Skip(11).Take(11).Select(x => x.Rule)));
            Assert.That(first.RuleResults, Has.All.Matches<GuardrailRuleResult>(x => x.PolicyVersion is not null && x.ObservedValue is not null && x.ThresholdValue is not null && x.ReasonCode is not null));
        });
    }

    [TestCase("identity", GuardrailReasonCodes.Unauthorized)]
    [TestCase("mandate", GuardrailReasonCodes.OutsideMandate)]
    [TestCase("price", GuardrailReasonCodes.PriceMissing)]
    [TestCase("liquidity", GuardrailReasonCodes.LiquidityUnknown)]
    [TestCase("market", GuardrailReasonCodes.MarketStateUnknown)]
    public void MissingOrUnauthorizedStateFailsRestrictively(string condition, string expectedReason)
    {
        var baseline = State();
        var state = baseline with
        {
            IdentityAuthorized = condition != "identity",
            WithinMandate = condition != "mandate",
            PriceObservedAt = condition == "price" ? null : baseline.PriceObservedAt,
            DailyLiquidity = condition == "liquidity" ? null : baseline.DailyLiquidity,
            MarketOpen = condition == "market" ? null : baseline.MarketOpen,
        };
        var result = HierarchicalGuardrailEvaluator.Evaluate(Proposal(), Policies(), state);
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(GuardrailOutcome.Failed));
            Assert.That(result.RuleResults.Any(x => x.ReasonCode == expectedReason), Is.True);
            Assert.That(result.RuleResults, Has.Count.EqualTo(44), "audit results remain complete after rejection");
        });
    }

    [Test]
    public void FinancialAndFreshnessBoundariesAreInclusive()
    {
        var now = UtcNow;
        var state = State() with
        {
            EvaluatedAt = now,
            PriceObservedAt = now.AddMinutes(-5),
            ResultingPositionNotional = Usd(1000),
            ResultingConcentration = new Percentage(20),
            AvailableCapital = Usd(110),
            DailyLiquidity = Usd(10000),
        };
        var result = HierarchicalGuardrailEvaluator.Evaluate(Proposal(), Policies(), state);
        Assert.That(result.Outcome, Is.EqualTo(GuardrailOutcome.Passed));
    }

    [Test]
    public void StalePriceAndExpiredProposalHaveStableReasons()
    {
        var proposal = Proposal(validUntil: UtcNow.AddMinutes(1));
        var result = HierarchicalGuardrailEvaluator.Evaluate(proposal, Policies(), State() with
        { EvaluatedAt = UtcNow.AddMinutes(6), PriceObservedAt = UtcNow });
        Assert.That(result.RuleResults.Select(x => x.ReasonCode), Does.Contain(GuardrailReasonCodes.PriceStale)
            .And.Contain(GuardrailReasonCodes.ProposalExpired));
    }

    internal static readonly DateTimeOffset UtcNow = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
    internal static Money Usd(decimal amount) => new(amount, Currency.USD);
    internal static TradeProposal Proposal(DateTimeOffset? validUntil = null, InstrumentId? instrument = null) => new(
        TradeProposalId.New(), TradingBotId.New(), BotRunId.New(), PortfolioId.New(), TradingBotConfigurationVersionId.New(),
        PortfolioDecisionSnapshotId.New(), instrument ?? InstrumentId.New(),
        new DirectTradeAction(TradeSide.Buy, new Quantity(1, "share"), ProposedOrderType.Market, null, ProposedTimeInForce.Day),
        "rationale", new ProposalContentVersion(1, "hash"), null, [], UtcNow.AddMinutes(-1), validUntil ?? UtcNow.AddHours(1));

    internal static GuardrailState State() => new(UtcNow, true, true, Usd(100), Usd(1000), new Percentage(20),
        Usd(110), UtcNow.AddMinutes(-5), Usd(10000), true);

    internal static HierarchicalGuardrailPolicySet Policies(decimal[]? maximums = null, decimal[]? reserves = null,
        bool[]? enabled = null, bool[]? requireOpen = null, InstrumentId[]?[]? instruments = null)
    {
        maximums ??= [1000, 1000, 1000, 1000]; reserves ??= [10, 10, 10, 10];
        enabled ??= [true, true, true, true]; requireOpen ??= [true, true, true, true];
        instruments ??= [null, null, null, null];
        var levels = Enum.GetValues<GuardrailPolicyLevel>();
        var values = levels.Select((level, index) => new GuardrailPolicy(new(level, $"policy-{index}", $"v{index + 1}"),
            enabled[index], instruments[index], Usd(maximums[index]), new Percentage(20), Usd(reserves[index]),
            TimeSpan.FromMinutes(5), Usd(10000), requireOpen[index])).ToArray();
        return new(values[0], values[1], values[2], values[3]);
    }
}
