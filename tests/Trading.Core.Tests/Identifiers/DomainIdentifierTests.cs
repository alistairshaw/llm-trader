using Trading.Core.Identifiers;

namespace Trading.Core.Tests.Identifiers;

[Category("Identifiers")]
public sealed class DomainIdentifierTests
{
    private const string CanonicalValue = "01HF7YAT00S8K1M3Q5V7X9ZBCE";

    private static readonly (string Name, Func<string, object> Parse, Func<object> New)[] IdentifierTypes =
    [
        (nameof(TradingBotId), value => TradingBotId.Parse(value), TradingBotId.New),
        (nameof(TradingBotConfigurationVersionId), value => TradingBotConfigurationVersionId.Parse(value), TradingBotConfigurationVersionId.New),
        (nameof(BotRunId), value => BotRunId.Parse(value), BotRunId.New),
        (nameof(BotRunTriggerId), value => BotRunTriggerId.Parse(value), BotRunTriggerId.New),
        (nameof(ToolInvocationId), value => ToolInvocationId.Parse(value), ToolInvocationId.New),
        (nameof(PortfolioId), value => PortfolioId.Parse(value), PortfolioId.New),
        (nameof(PositionId), value => PositionId.Parse(value), PositionId.New),
        (nameof(PortfolioDecisionSnapshotId), value => PortfolioDecisionSnapshotId.Parse(value), PortfolioDecisionSnapshotId.New),
        (nameof(PortfolioLedgerEntryId), value => PortfolioLedgerEntryId.Parse(value), PortfolioLedgerEntryId.New),
        (nameof(BrokerConnectionId), value => BrokerConnectionId.Parse(value), BrokerConnectionId.New),
        (nameof(BrokerAccountId), value => BrokerAccountId.Parse(value), BrokerAccountId.New),
        (nameof(InstrumentId), value => InstrumentId.Parse(value), InstrumentId.New),
        (nameof(InstrumentBrokerMappingId), value => InstrumentBrokerMappingId.Parse(value), InstrumentBrokerMappingId.New),
        (nameof(ResearchRequestId), value => ResearchRequestId.Parse(value), ResearchRequestId.New),
        (nameof(ResearchSubscriptionId), value => ResearchSubscriptionId.Parse(value), ResearchSubscriptionId.New),
        (nameof(ResearchReportId), value => ResearchReportId.Parse(value), ResearchReportId.New),
        (nameof(HypothesisId), value => HypothesisId.Parse(value), HypothesisId.New),
        (nameof(HypothesisVersionId), value => HypothesisVersionId.Parse(value), HypothesisVersionId.New),
        (nameof(TradeProposalId), value => TradeProposalId.Parse(value), TradeProposalId.New),
        (nameof(GuardrailEvaluationId), value => GuardrailEvaluationId.Parse(value), GuardrailEvaluationId.New),
        (nameof(ProposalApprovalId), value => ProposalApprovalId.Parse(value), ProposalApprovalId.New),
        (nameof(CapitalReservationId), value => CapitalReservationId.Parse(value), CapitalReservationId.New),
        (nameof(OrderId), value => OrderId.Parse(value), OrderId.New),
        (nameof(OrderTransitionId), value => OrderTransitionId.Parse(value), OrderTransitionId.New),
        (nameof(FillId), value => FillId.Parse(value), FillId.New),
    ];

    [TestCaseSource(nameof(IdentifierTypes))]
    public void ParsingAndFormattingRoundTrip(
        (string Name, Func<string, object> Parse, Func<object> New) identifierType)
    {
        var identifier = identifierType.Parse(CanonicalValue.ToLowerInvariant());

        Assert.That(identifier.ToString(), Is.EqualTo(CanonicalValue), identifierType.Name);
        Assert.That(identifierType.Parse(identifier.ToString()!), Is.EqualTo(identifier), identifierType.Name);
    }

    [TestCaseSource(nameof(IdentifierTypes))]
    public void GenerationProducesCanonicalNonEmptyUlid(
        (string Name, Func<string, object> Parse, Func<object> New) identifierType)
    {
        var first = identifierType.New();
        var second = identifierType.New();

        Assert.Multiple(() =>
        {
            Assert.That(first.ToString(), Has.Length.EqualTo(26), identifierType.Name);
            Assert.That(first.ToString(), Does.Match("^[0-7][0-9A-HJKMNP-TV-Z]{25}$"), identifierType.Name);
            Assert.That(second, Is.Not.EqualTo(first), identifierType.Name);
        });
    }

    [TestCaseSource(nameof(IdentifierTypes))]
    public void InvalidValuesAreRejected(
        (string Name, Func<string, object> Parse, Func<object> New) identifierType)
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => identifierType.Parse(string.Empty), Throws.ArgumentException, identifierType.Name);
            Assert.That(() => identifierType.Parse("00000000000000000000000000"), Throws.ArgumentException, identifierType.Name);
            Assert.That(() => identifierType.Parse("01HF7YAT00S8K1M3Q5V7X9ZBCI"), Throws.ArgumentException, identifierType.Name);
            Assert.That(() => identifierType.Parse("81HF7YAT00S8K1M3Q5V7X9ZBCE"), Throws.ArgumentException, identifierType.Name);
        });
    }

    [Test]
    public void UnrelatedIdentifierTypesRemainDistinct()
    {
        var tradingBotId = TradingBotId.Parse(CanonicalValue);
        var portfolioId = PortfolioId.Parse(CanonicalValue);

        Assert.That(tradingBotId.GetType(), Is.Not.EqualTo(portfolioId.GetType()));
        Assert.That(AcceptTradingBotId(tradingBotId), Is.SameAs(tradingBotId));
    }

    [Test]
    public void TypedGeneratorProvidesDeterministicTestSeam()
    {
        IIdentifierGenerator<TradingBotId> generator =
            new FixedIdentifierGenerator<TradingBotId>(TradingBotId.Parse(CanonicalValue));

        Assert.That(generator.Generate().ToString(), Is.EqualTo(CanonicalValue));
    }

    private static TradingBotId AcceptTradingBotId(TradingBotId identifier) => identifier;

    private sealed class FixedIdentifierGenerator<TIdentifier>(TIdentifier identifier) : IIdentifierGenerator<TIdentifier>
        where TIdentifier : notnull
    {
        public TIdentifier Generate() => identifier;
    }
}
