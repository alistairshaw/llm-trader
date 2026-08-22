namespace Trading.Core.Identifiers;

public sealed record TradingBotId
{
    private TradingBotId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static TradingBotId New() => new(DomainIdentifierValue.New());
    public static TradingBotId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record TradingBotConfigurationVersionId
{
    private TradingBotConfigurationVersionId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static TradingBotConfigurationVersionId New() => new(DomainIdentifierValue.New());
    public static TradingBotConfigurationVersionId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record BotRunId
{
    private BotRunId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static BotRunId New() => new(DomainIdentifierValue.New());
    public static BotRunId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record BotRunTriggerId
{
    private BotRunTriggerId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static BotRunTriggerId New() => new(DomainIdentifierValue.New());
    public static BotRunTriggerId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record ToolInvocationId
{
    private ToolInvocationId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static ToolInvocationId New() => new(DomainIdentifierValue.New());
    public static ToolInvocationId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record PortfolioId
{
    private PortfolioId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static PortfolioId New() => new(DomainIdentifierValue.New());
    public static PortfolioId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record PositionId
{
    private PositionId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static PositionId New() => new(DomainIdentifierValue.New());
    public static PositionId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record PortfolioDecisionSnapshotId
{
    private PortfolioDecisionSnapshotId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static PortfolioDecisionSnapshotId New() => new(DomainIdentifierValue.New());
    public static PortfolioDecisionSnapshotId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record PortfolioLedgerEntryId
{
    private PortfolioLedgerEntryId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static PortfolioLedgerEntryId New() => new(DomainIdentifierValue.New());
    public static PortfolioLedgerEntryId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record BrokerConnectionId
{
    private BrokerConnectionId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static BrokerConnectionId New() => new(DomainIdentifierValue.New());
    public static BrokerConnectionId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record BrokerAccountId
{
    private BrokerAccountId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static BrokerAccountId New() => new(DomainIdentifierValue.New());
    public static BrokerAccountId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record InstrumentId
{
    private InstrumentId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static InstrumentId New() => new(DomainIdentifierValue.New());
    public static InstrumentId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record InstrumentBrokerMappingId
{
    private InstrumentBrokerMappingId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static InstrumentBrokerMappingId New() => new(DomainIdentifierValue.New());
    public static InstrumentBrokerMappingId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record ResearchRequestId
{
    private ResearchRequestId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static ResearchRequestId New() => new(DomainIdentifierValue.New());
    public static ResearchRequestId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record ResearchSubscriptionId
{
    private ResearchSubscriptionId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static ResearchSubscriptionId New() => new(DomainIdentifierValue.New());
    public static ResearchSubscriptionId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record ResearchReportId
{
    private ResearchReportId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static ResearchReportId New() => new(DomainIdentifierValue.New());
    public static ResearchReportId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record ResearchRunAttemptId
{
    private ResearchRunAttemptId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static ResearchRunAttemptId New() => new(DomainIdentifierValue.New());
    public static ResearchRunAttemptId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record HypothesisId
{
    private HypothesisId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static HypothesisId New() => new(DomainIdentifierValue.New());
    public static HypothesisId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record HypothesisVersionId
{
    private HypothesisVersionId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static HypothesisVersionId New() => new(DomainIdentifierValue.New());
    public static HypothesisVersionId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record TradeProposalId
{
    private TradeProposalId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static TradeProposalId New() => new(DomainIdentifierValue.New());
    public static TradeProposalId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record GuardrailEvaluationId
{
    private GuardrailEvaluationId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static GuardrailEvaluationId New() => new(DomainIdentifierValue.New());
    public static GuardrailEvaluationId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record ProposalApprovalId
{
    private ProposalApprovalId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static ProposalApprovalId New() => new(DomainIdentifierValue.New());
    public static ProposalApprovalId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record CapitalReservationId
{
    private CapitalReservationId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static CapitalReservationId New() => new(DomainIdentifierValue.New());
    public static CapitalReservationId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record OrderId
{
    private OrderId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static OrderId New() => new(DomainIdentifierValue.New());
    public static OrderId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record OrderTransitionId
{
    private OrderTransitionId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static OrderTransitionId New() => new(DomainIdentifierValue.New());
    public static OrderTransitionId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record FillId
{
    private FillId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static FillId New() => new(DomainIdentifierValue.New());
    public static FillId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record BrokerMessageId
{
    private BrokerMessageId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static BrokerMessageId New() => new(DomainIdentifierValue.New());
    public static BrokerMessageId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}

public sealed record OrderWorkItemId
{
    private OrderWorkItemId(DomainIdentifierValue value) => Value = value;
    private DomainIdentifierValue Value { get; }
    public static OrderWorkItemId New() => new(DomainIdentifierValue.New());
    public static OrderWorkItemId Parse(string value) => new(DomainIdentifierValue.Parse(value, nameof(value)));
    public override string ToString() => Value.ToString();
}
