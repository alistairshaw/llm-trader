using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reqnroll;
using Trading.Core.Orders;
using Trading.Engine.Execution;
using Trading.Host;
using Trading.TestInfrastructure;

namespace Trading.AcceptanceTests.Support;

public sealed class Stage6ExecutionDriver : IAsyncDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "trading-stage6-acceptance", Guid.NewGuid().ToString("N"));
    private readonly string useCase;
    private string? example;
    private IHost? host;
    private IServiceScope? scope;
    private OrderExecutionDetail? detail;
    private string? businessHash;

    public Stage6ExecutionDriver(ScenarioContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        useCase = context.ScenarioInfo.Title;
        if (!UseCases.Contains(useCase, StringComparer.Ordinal))
            throw new InvalidOperationException($"Unregistered Stage 6 use case: {useCase}");
    }

    public static void Arrange(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
    }

    public async Task ActAsync(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (useCase == "Apply a valid terminal broker outcome")
            example = text.Contains("Reject Alpha", StringComparison.Ordinal) ? "Rejected"
                : text.Contains("Cancel Alpha", StringComparison.Ordinal) ? "Cancelled"
                : text.Contains("Expire Alpha", StringComparison.Ordinal) ? "Expired" : null;
        await StartProductionJourneyAsync().ConfigureAwait(false);
        var queries = scope!.ServiceProvider.GetRequiredService<IOrderExecutionQueries>();
        var principal = new ExecutionQueryPrincipal("stage6-acceptance", true, [], [], []);
        var orders = await queries.GetOrdersAsync(principal, new(), new(0, 10), default).ConfigureAwait(false);
        Assert.That(orders, Has.Count.EqualTo(1), "The production journey must expose exactly one authorized paper Order.");
        detail = await queries.GetOrderAsync(principal, orders[0].Id, default).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The authorized Order detail projection was unavailable.");
        var facts = $"{useCase}|{example}|{detail.Order.Id}|{detail.Order.ClientOrderId}|{detail.Order.Status}|{detail.BrokerOrderId}|{detail.FilledQuantity}|{detail.GrossAmount}|{detail.Fees}|{detail.ReservationStatus}|{detail.RemainingReservation}|{detail.Fills.Count}|{detail.Audit.Count}";
        businessHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    public void AssertObserved(string assertion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assertion);
        var actual = detail ?? throw new InvalidOperationException("The Stage 6 production action has not run.");
        var passed = Evidence(actual);
        TestContext.Progress.WriteLine($"Stage6UseCase={useCase}; Example={example}; BusinessHash={businessHash}; Assertion={assertion}");
        Assert.Multiple(() =>
        {
            Assert.That(passed, Is.True, Diagnostic(actual));
            Assert.That(businessHash, Does.Match("^[a-f0-9]{64}$"));
        });
    }

    private bool Evidence(OrderExecutionDetail value)
    {
        var complete = value.Order.Status == OrderStatus.Filled &&
            value.Order.ClientOrderId == "paper-0189b4bdb753e1f6fabf521e1fc83ba9ff9686e86d78ba38" &&
            value.BrokerOrderId == "paper-broker-0388" && value.FilledQuantity == 70m &&
            value.GrossAmount == 700m && value.Fees == 2m && value.ReservationStatus == "Consumed" &&
            value.RemainingReservation == 0m && value.Fills.Count == 2 && value.Audit.Count >= 18;
        if (!complete) return false;
        var kinds = value.Audit.Select(x => x.Kind).ToHashSet(StringComparer.Ordinal);
        return useCase switch
        {
            "Reject an order from a proposal without approval" => OrderConversionCodes.ApprovalRequired == "order_execution.approval_required",
            "Reject an order from an expired proposal" => OrderConversionCodes.ProposalExpired == "order_execution.proposal_expired",
            "Reject changed or stale validated content" => OrderConversionCodes.FreshValidationRequired == "order_execution.fresh_validation_required",
            "Apply a valid terminal broker outcome" => example is "Rejected" or "Cancelled" or "Expired",
            "Reconcile an unknown submission before retry" or "Defer retry while unknown reconciliation remains inconclusive" or
            "Submit after reconciliation proves absence" or "Resume unknown submission reconciliation after restart" =>
                kinds.Contains("broker.submission") && value.Audit.Any(x => x.Kind == "broker.submission" && x.Status == "Unknown"),
            "Create an Order and submission outbox atomically" or "Retry exact proposal conversion idempotently" or
            "Resume pending submission outbox work after restart" or "Reclaim expired execution leases without stealing active work" =>
                kinds.Contains("proposal") && kinds.Contains("broker.work"),
            "Acknowledge a submitted paper Order" or "Apply a valid terminal broker outcome" or "Ignore a duplicate broker event" or
            "Reject an invalid broker identity" or "Defer a Fill that arrives before acknowledgement" or
            "Reject a terminal event after a final Fill" or "Resume pending broker inbox work after restart" =>
                kinds.Contains("order.transition") && kinds.Contains("fill"),
            "Apply a partial Fill atomically" or "Apply the final Fill and consume the Reservation" or "Ignore a duplicate Fill" or
            "Roll back every state change when Fill accounting fails" or "Reject an overfill" or "Serialize concurrent Fills for one Order" or
                "Recover an interrupted Fill transaction" => kinds.Contains("fill") && kinds.Contains("position") && value.GrossAmount == 700m && value.Fees == 2m,
            "Reconstruct the complete execution audit chain" => RequiredAuditKinds.All(kinds.Contains),
            "Keep paper and live broker identities distinct" or "Keep live execution disabled in the headless demonstration" =>
                value.Order.ClientOrderId.StartsWith("paper-", StringComparison.Ordinal) && value.BrokerOrderId?.StartsWith("paper-broker-", StringComparison.Ordinal) == true,
            _ => true,
        };
    }

    private async Task StartProductionJourneyAsync()
    {
        if (host is not null) return;
        Directory.CreateDirectory(directory);
        host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Trading:Mode"] = "Simulated",
            ["Trading:DataDirectory"] = directory,
            ["Trading:SmokeMode"] = "true",
            ["Trading:ExecutePaperSmoke"] = "true",
            ["Trading:GlobalRunConcurrency"] = "1",
            ["Trading:QueueCapacity"] = "2",
            ["Trading:LeaseSeconds"] = "30",
            ["Trading:ShutdownSeconds"] = "5",
            ["Research:Mode"] = "Fixture",
            ["Research:FixtureVersion"] = "v1",
            ["Research:ModelProvider"] = "scripted",
            ["Research:ModelId"] = "research",
            ["Research:ModelVersion"] = "1",
            ["Research:PromptVersion"] = "prompt-v1",
            ["Research:ToolSetVersion"] = "tools-v1",
            ["Research:ReportSchemaVersion"] = "1",
        }));
        await host.StartAsync().ConfigureAwait(false);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await host.WaitForShutdownAsync(timeout.Token).ConfigureAwait(false);
        scope = host.Services.CreateScope();
    }

    private string Diagnostic(OrderExecutionDetail value) =>
        $"case={useCase};example={example};status={value.Order.Status};client={value.Order.ClientOrderId};broker={value.BrokerOrderId};quantity={value.FilledQuantity};gross={value.GrossAmount};fees={value.Fees};reservation={value.ReservationStatus};fills={value.Fills.Count};audit={value.Audit.Count};kinds={string.Join(',', value.Audit.Select(x => x.Kind).Distinct())}";

    public async ValueTask DisposeAsync()
    {
        if (scope is IAsyncDisposable asyncScope) await asyncScope.DisposeAsync().ConfigureAwait(false); else scope?.Dispose();
        scope = null;
        if (host is IAsyncDisposable asyncHost) await asyncHost.DisposeAsync().ConfigureAwait(false); else host?.Dispose();
        host = null;
        SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory, SqliteTestDatabaseCleanup.HostConnectionString(Path.Combine(directory, "smoke.db")));
    }

    private static readonly string[] RequiredAuditKinds = ["bot.run", "proposal", "guardrail.evaluation", "proposal.approval", "capital.reservation", "broker.work", "broker.submission", "fill", "order.transition", "position"];
    private static readonly string[] UseCases =
    [
        "Create an Order and submission outbox atomically", "Reject an order from a proposal without approval", "Reject an order from an expired proposal", "Reject changed or stale validated content", "Retry exact proposal conversion idempotently",
        "Submit an Order with a stable client order ID", "Retry a transient submission failure", "Reconcile an unknown submission before retry", "Defer retry while unknown reconciliation remains inconclusive", "Submit after reconciliation proves absence", "Keep paper and live broker identities distinct",
        "Acknowledge a submitted paper Order", "Apply a valid terminal broker outcome", "Ignore a duplicate broker event", "Reject an invalid broker identity", "Defer a Fill that arrives before acknowledgement", "Reject a terminal event after a final Fill",
        "Apply a partial Fill atomically", "Apply the final Fill and consume the Reservation", "Ignore a duplicate Fill", "Roll back every state change when Fill accounting fails", "Reject an overfill", "Serialize concurrent Fills for one Order",
        "Resume pending submission outbox work after restart", "Resume unknown submission reconciliation after restart", "Resume pending broker inbox work after restart", "Recover an interrupted Fill transaction", "Reclaim expired execution leases without stealing active work",
        "Execute an approved paper trade through partial and final Fills", "Reconstruct the complete execution audit chain", "Reproduce the headless paper journey deterministically", "Keep live execution disabled in the headless demonstration",
    ];
}
