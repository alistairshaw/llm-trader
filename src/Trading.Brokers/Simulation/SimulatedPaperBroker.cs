using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;

namespace Trading.Brokers.Simulation;

public interface ISimulatedBrokerClock { DateTimeOffset UtcNow { get; } }
public interface ISimulatedBrokerLatency
{
    Task WaitAsync(string operation, CancellationToken cancellationToken);
}

public interface ISimulatedBrokerIdentifierSource
{
    string NewBrokerOrderId();
    BrokerMessageId NewMessageId();
    string NewExecutionId();
}

public sealed record SimulatedExecutionScript(Quantity Quantity, Price Price, Money Fee);
public sealed record SimulatedEventScript(BrokerOrderEventKind Kind, string Code, SimulatedExecutionScript? Execution = null,
    int? DuplicateOf = null);

public enum SimulatedSubmissionBehavior { Accept, Reject, TimeoutAfterAcceptance, Unknown }

public sealed record SimulatedOrderScript(
    SimulatedSubmissionBehavior Submission,
    IReadOnlyList<SimulatedEventScript> Events,
    BrokerCancellationOutcome Cancellation = BrokerCancellationOutcome.Accepted)
{
    public static SimulatedOrderScript Accepted(params SimulatedEventScript[] events) =>
        new(SimulatedSubmissionBehavior.Accept, events);
}

public sealed record SimulatedBrokerOrderState(ClientOrderIdentity ClientOrderId, string BrokerOrderId,
    BrokerOrderRequest Request, OrderStatus Status);

public sealed class SimulatedPaperBroker : IPaperBrokerGateway
{
    private const string EnvironmentMismatch = "broker.paper_environment_mismatch";
    private readonly object sync = new();
    private readonly BrokerConnectionId connectionId;
    private readonly BrokerAccountId accountId;
    private readonly string environmentName;
    private readonly ISimulatedBrokerClock clock;
    private readonly ISimulatedBrokerIdentifierSource identifiers;
    private readonly ISimulatedBrokerLatency latency;
    private readonly Dictionary<string, SimulatedOrderScript> scripts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoredOrder> orders = new(StringComparer.Ordinal);

    public SimulatedPaperBroker(BrokerConnectionId connectionId, BrokerAccountId accountId, string environmentName,
        ISimulatedBrokerClock clock, ISimulatedBrokerIdentifierSource identifiers, ISimulatedBrokerLatency latency)
    {
        this.connectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
        this.accountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
        this.environmentName = string.IsNullOrWhiteSpace(environmentName)
            ? throw new ArgumentException("Paper environment name is required.", nameof(environmentName))
            : environmentName.Trim();
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.identifiers = identifiers ?? throw new ArgumentNullException(nameof(identifiers));
        this.latency = latency ?? throw new ArgumentNullException(nameof(latency));
    }

    public BrokerCapabilities Capabilities => BrokerCapabilities.SubmitMarketOrders |
        BrokerCapabilities.SubmitLimitOrders | BrokerCapabilities.LookupByClientOrderId |
        BrokerCapabilities.ReconcileOrderStatus | BrokerCapabilities.CancelOrders |
        BrokerCapabilities.StreamExecutions;

    public void Configure(ClientOrderIdentity clientOrderId, SimulatedOrderScript script)
    {
        ArgumentNullException.ThrowIfNull(clientOrderId);
        ArgumentNullException.ThrowIfNull(script);
        ValidateScript(script);
        lock (sync)
        {
            if (orders.ContainsKey(clientOrderId.Value))
                throw new InvalidOperationException("A script cannot change after submission.");
            scripts[clientOrderId.Value] = script;
        }
    }

    public async Task<BrokerSubmissionResult> SubmitAsync(PaperBrokerOperationContext context,
        BrokerOrderRequest request, CancellationToken cancellationToken)
    {
        ValidateContext(context);
        ArgumentNullException.ThrowIfNull(request);
        await latency.WaitAsync("submit", cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (orders.TryGetValue(request.ClientOrderId.Value, out var existing))
            {
                if (existing.Request != request)
                    return new(BrokerSubmissionOutcome.TerminalFailure, BrokerExecutionCodes.Terminal, null, clock.UtcNow);
                return new(BrokerSubmissionOutcome.Duplicate, BrokerExecutionCodes.Duplicate,
                    existing.BrokerOrderId, clock.UtcNow);
            }

            var script = scripts.GetValueOrDefault(request.ClientOrderId.Value) ?? SimulatedOrderScript.Accepted();
            if (script.Submission == SimulatedSubmissionBehavior.Reject)
                return new(BrokerSubmissionOutcome.Rejected, BrokerExecutionCodes.Rejected, null, clock.UtcNow);
            if (script.Submission == SimulatedSubmissionBehavior.Unknown)
                return new(BrokerSubmissionOutcome.Unknown, BrokerExecutionCodes.Unknown, null, clock.UtcNow);

            var brokerOrderId = identifiers.NewBrokerOrderId();
            var stored = new StoredOrder(request, brokerOrderId, script);
            foreach (var item in script.Events)
                stored.Pending.Add(item.DuplicateOf is int duplicate ? stored.Pending[duplicate] : CreateEvent(stored, item));
            orders.Add(request.ClientOrderId.Value, stored);
            return script.Submission == SimulatedSubmissionBehavior.TimeoutAfterAcceptance
                ? new(BrokerSubmissionOutcome.Unknown, BrokerExecutionCodes.Unknown, null, clock.UtcNow)
                : new(BrokerSubmissionOutcome.Accepted, BrokerExecutionCodes.Accepted, brokerOrderId, clock.UtcNow);
        }
    }

    public Task<BrokerReconciliationResult> FindByClientOrderIdAsync(PaperBrokerOperationContext context,
        BrokerOrderLookup lookup, CancellationToken cancellationToken) => LookupAsync("lookup", context, lookup, cancellationToken);

    public Task<BrokerReconciliationResult> ReconcileAsync(PaperBrokerOperationContext context,
        BrokerOrderLookup lookup, CancellationToken cancellationToken) => LookupAsync("reconcile", context, lookup, cancellationToken);

    public async Task<BrokerCancellationResult> CancelAsync(PaperBrokerOperationContext context,
        BrokerCancellationRequest request, CancellationToken cancellationToken)
    {
        ValidateContext(context);
        ArgumentNullException.ThrowIfNull(request);
        await latency.WaitAsync("cancel", cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (!orders.TryGetValue(request.ClientOrderId.Value, out var order) || order.BrokerOrderId != request.BrokerOrderId)
                return new(BrokerCancellationOutcome.Rejected, BrokerExecutionCodes.Rejected, clock.UtcNow);
            if (order.Status is OrderStatus.Cancelled or OrderStatus.Expired or OrderStatus.Rejected or OrderStatus.Filled)
                return new(BrokerCancellationOutcome.AlreadyTerminal, BrokerExecutionCodes.Terminal, clock.UtcNow);
            if (order.Script.Cancellation != BrokerCancellationOutcome.Accepted)
                return new(order.Script.Cancellation, CancellationCode(order.Script.Cancellation), clock.UtcNow);
            order.Status = OrderStatus.Cancelled;
            order.Pending.Add(CreateEvent(order, new(BrokerOrderEventKind.Cancelled, "broker.cancelled")));
            return new(BrokerCancellationOutcome.Accepted, BrokerExecutionCodes.Accepted, clock.UtcNow);
        }
    }

    public async Task<IReadOnlyList<BrokerOrderEvent>> ReadEventsAsync(PaperBrokerOperationContext context,
        CancellationToken cancellationToken)
    {
        ValidateContext(context);
        await latency.WaitAsync("events", cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            var events = orders.Values.SelectMany(order => order.Pending.Select(item => (order, item))).ToArray();
            foreach (var (order, item) in events) Apply(order, item);
            foreach (var order in orders.Values) order.Pending.Clear();
            return events.Select(value => value.item).ToArray();
        }
    }

    public IReadOnlyList<SimulatedBrokerOrderState> Snapshot(PaperBrokerOperationContext context)
    {
        ValidateContext(context);
        lock (sync)
            return orders.Values.Select(order => new SimulatedBrokerOrderState(order.Request.ClientOrderId,
                order.BrokerOrderId, order.Request, order.Status)).ToArray();
    }

    private async Task<BrokerReconciliationResult> LookupAsync(string operation, PaperBrokerOperationContext context,
        BrokerOrderLookup lookup, CancellationToken cancellationToken)
    {
        ValidateContext(context);
        ArgumentNullException.ThrowIfNull(lookup);
        await latency.WaitAsync(operation, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
            return orders.TryGetValue(lookup.ClientOrderId.Value, out var order)
                ? new(BrokerReconciliationOutcome.Found, BrokerExecutionCodes.ReconciledFound,
                    order.BrokerOrderId, order.Status, clock.UtcNow)
                : new(BrokerReconciliationOutcome.Absent, BrokerExecutionCodes.ReconciledAbsent,
                    null, null, clock.UtcNow);
    }

    private void ValidateContext(PaperBrokerOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.BrokerConnectionId != connectionId || context.BrokerAccountId != accountId ||
            !string.Equals(context.Environment.Name, environmentName, StringComparison.Ordinal))
            throw new InvalidOperationException(EnvironmentMismatch);
    }

    private static void ValidateScript(SimulatedOrderScript script)
    {
        if (script.Events.Count > 100) throw new ArgumentException("A simulator script is bounded to 100 events.", nameof(script));
        for (var index = 0; index < script.Events.Count; index++)
        {
            var item = script.Events[index];
            if (item.DuplicateOf is null && item.Kind == BrokerOrderEventKind.Execution != (item.Execution is not null))
                throw new ArgumentException("Only execution events carry execution detail.", nameof(script));
            if (item.DuplicateOf is int duplicate && (duplicate < 0 || duplicate >= index))
                throw new ArgumentException("Duplicate events must reference an earlier event.", nameof(script));
        }
    }

    private BrokerOrderEvent CreateEvent(StoredOrder order, SimulatedEventScript script)
    {
        var execution = script.Execution is null ? null : new BrokerExecution(identifiers.NewExecutionId(),
            script.Execution.Quantity, script.Execution.Price, script.Execution.Fee, clock.UtcNow);
        return new(identifiers.NewMessageId(), order.Request.ClientOrderId, order.BrokerOrderId, script.Kind,
            script.Code, execution, clock.UtcNow, clock.UtcNow);
    }

    private static void Apply(StoredOrder order, BrokerOrderEvent item)
    {
        if (item.Execution is not null && !order.Executions.Add(item.Execution.ExecutionId)) return;
        if (item.Execution is not null) order.FilledQuantity += item.Execution.Quantity.Amount;
        var candidate = item.Kind switch
        {
            BrokerOrderEventKind.Acknowledged => OrderStatus.Acknowledged,
            BrokerOrderEventKind.Rejected => OrderStatus.Rejected,
            BrokerOrderEventKind.Cancelled => OrderStatus.Cancelled,
            BrokerOrderEventKind.Expired => OrderStatus.Expired,
            BrokerOrderEventKind.Execution when order.FilledQuantity >= order.Request.Quantity.Amount => OrderStatus.Filled,
            BrokerOrderEventKind.Execution => OrderStatus.PartiallyFilled,
            _ => order.Status,
        };
        if (order.Status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Expired or OrderStatus.Rejected) return;
        if (candidate == OrderStatus.Acknowledged && order.Status == OrderStatus.PartiallyFilled) return;
        order.Status = candidate;
    }

    private static string CancellationCode(BrokerCancellationOutcome outcome) => outcome switch
    {
        BrokerCancellationOutcome.Unknown => BrokerExecutionCodes.Unknown,
        BrokerCancellationOutcome.RetryableFailure => BrokerExecutionCodes.Retryable,
        BrokerCancellationOutcome.TerminalFailure => BrokerExecutionCodes.Terminal,
        BrokerCancellationOutcome.Rejected => BrokerExecutionCodes.Rejected,
        _ => BrokerExecutionCodes.Terminal,
    };

    private sealed class StoredOrder
    {
        public StoredOrder(BrokerOrderRequest request, string brokerOrderId, SimulatedOrderScript script)
        {
            Request = request;
            BrokerOrderId = brokerOrderId;
            Script = script;
            Pending = [];
        }

        public BrokerOrderRequest Request { get; }
        public string BrokerOrderId { get; }
        public SimulatedOrderScript Script { get; }
        public OrderStatus Status { get; set; } = OrderStatus.Submitted;
        public decimal FilledQuantity { get; set; }
        public HashSet<string> Executions { get; } = new(StringComparer.Ordinal);
        public List<BrokerOrderEvent> Pending { get; }

    }
}
