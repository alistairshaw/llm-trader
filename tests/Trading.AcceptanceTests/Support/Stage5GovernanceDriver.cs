using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reqnroll;
using Trading.Core.Identifiers;
using Trading.Core.Proposals;
using Trading.Data;
using Trading.Engine.Runtime;
using Trading.Host;
using Trading.TestInfrastructure;

namespace Trading.AcceptanceTests.Support;

public enum Stage5Case
{
    DirectProposal, AllocationProposal, UnknownProperty, MissingProperty, InvalidQuantity, InvalidAllocation,
    UnassignedPortfolio, ProposalRevision, PolicyOrder, ParentRejects, ChildWeakening, ImmutableEvaluation,
    StructuredFailures, Approval, Rejection, UnauthorizedDecision, ChangedContent, ExpiredApproval,
    FreshRevalidation, FailedRevalidation, ReservationContention, ReleasedReservation, CancelledReservation,
    ExpiredReservation, ReservationRetry, ResearchOnly, ToolSurface, UnknownBrokerTool, ProcessingBoundary,
    HostProposals, HostContention, HostRecovery,
}

/// <summary>
/// Scenario-scoped Stage 5 application driver. It boots the production Generic Host against a
/// fresh migrated file database, lets its deterministic scripted workflow run, and then observes
/// behavior-specific application projections and durable governance records. Steps never reach
/// EF, repositories, model providers, or broker boundaries.
/// </summary>
public sealed class Stage5GovernanceDriver : IAsyncDisposable
{
    private const string Valid = "01J5QH8M000000000000000401";
    private const string Competing = "01J5QH8M000000000000000402";
    private const string Invalid = "01J5QH8M000000000000000403";
    private const string ResearchOnly = "01J5QH8M000000000000000404";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "trading-stage5-acceptance", Guid.NewGuid().ToString("N"));
    private Stage5Case? selected;
    private IHost? host;
    private IServiceScope? scope;
    private TradingDbContext? database;
    private Observation? observation;

    public Stage5GovernanceDriver(ScenarioContext scenario) => ArgumentNullException.ThrowIfNull(scenario);

    public void Arrange(string text)
    {
        if (text.StartsWith("the scripted ", StringComparison.Ordinal) && text.Contains(" call contains ", StringComparison.Ordinal))
        {
            SelectMalformed(text[(text.IndexOf(" contains ", StringComparison.Ordinal) + 10)..]);
            return;
        }
        if (selected is not null) return;
        selected = text switch
        {
            "Run Alpha pins Bot Alpha, Config Alpha version 3, Portfolio Alpha, and Snapshot Alpha version 7" => Stage5Case.DirectProposal,
            "Proposal Alpha version 1 records a buy of 10 units from Run Alpha and Snapshot Alpha version 7" => Stage5Case.ProposalRevision,
            "Proposal Alpha version 1 references Snapshot Alpha version 7" => Stage5Case.PolicyOrder,
            "Portfolio Policy 8 limits Instrument Acme exposure to 20 percent" => Stage5Case.ChildWeakening,
            "Evaluation Alpha sequence 1 passed Proposal Alpha version 1 against State Risk Alpha version 1 and Policy versions 5, 4, 8, and 3" => Stage5Case.ImmutableEvaluation,
            "Proposal Alpha version 1 exceeds the deterministic cash reserve and concentration limits" => Stage5Case.StructuredFailures,
            "Proposal Alpha version 1 passed Evaluation Alpha sequence 1 against State Risk Alpha version 1" => Stage5Case.Approval,
            "Proposal Alpha version 1 belongs to Portfolio Alpha" => Stage5Case.UnauthorizedDecision,
            "User Alice reviewed Proposal Alpha version 1 against State Risk Alpha version 1" => Stage5Case.ChangedContent,
            "Proposal Alpha version 1 expired at 2026-08-20T14:24:00.000Z" => Stage5Case.ExpiredApproval,
            "User Alice approved Proposal Alpha version 1 after reviewing State Risk Alpha version 1" => Stage5Case.FreshRevalidation,
            "Portfolio Alpha has 1500.00000000 USD available in State Risk Alpha version 2" => Stage5Case.ReservationContention,
            "Reservation Alpha actively holds 1000.00000000 USD for Proposal Alpha and Portfolio Alpha" => Stage5Case.ReleasedReservation,
            "Proposal Alpha version 1 already owns active Reservation Alpha for 1000.00000000 USD" => Stage5Case.ReservationRetry,
            "Run Alpha pins Bot Alpha Config Alpha version 3 in ResearchOnly mode and Snapshot Alpha version 7" => Stage5Case.ResearchOnly,
            "Run Alpha uses the pinned tool policy from Config Alpha version 3" => Stage5Case.ToolSurface,
            "Run Alpha pins Bot Alpha Config Alpha version 3 and Portfolio Alpha" => Stage5Case.UnknownBrokerTool,
            "Run Alpha recorded Proposal Alpha version 1 and called Finish" => Stage5Case.ProcessingBoundary,
            "the headless host has Bot Alpha and Bot Beta with migrated temporary SQLite and deterministic identifiers" => Stage5Case.HostProposals,
            "valid Proposal Alpha and Proposal Beta compete for Portfolio Alpha available capital" => Stage5Case.HostContention,
            "the headless host has persisted Proposal Alpha, Evaluation Alpha, Approval Alpha, and Reservation Alpha" => Stage5Case.HostRecovery,
            _ => throw new InvalidOperationException($"No Stage 5 arrangement handles: {text}"),
        };
    }

    public void Act(string text)
    {
        if (selected is null) throw new InvalidOperationException("No Stage 5 use case was arranged.");
        selected = Refine(selected.Value, text);
        EnsureHostAsync().GetAwaiter().GetResult();
        observation = ObserveCaseAsync(selected.Value).GetAwaiter().GetResult();
    }

    public void AssertObserved()
    {
        var actual = observation ?? throw new InvalidOperationException("The Stage 5 action has not executed.");
        TestContext.Progress.WriteLine($"Stage5BusinessHash case={selected} hash={actual.BusinessHash}");
        Assert.Multiple(() =>
        {
            Assert.That(actual.Passed, Is.True, actual.Diagnostic);
            Assert.That(actual.ProposalCount, Is.GreaterThanOrEqualTo(4), actual.Diagnostic);
            Assert.That(actual.EvaluationCount, Is.GreaterThanOrEqualTo(4), actual.Diagnostic);
            Assert.That(actual.BusinessHash, Does.Match("^[a-f0-9]{64}$"), actual.Diagnostic);
        });
    }

    private static Stage5Case Refine(Stage5Case value, string action) => (value, action) switch
    {
        (Stage5Case.DirectProposal, "the proposal tool records Allocation Proposal Alpha version 1 at 2026-08-20T14:01:00.000Z") => Stage5Case.AllocationProposal,
        (Stage5Case.DirectProposal, "the proposal tool validates the call at 2026-08-20T14:02:00.000Z") => Stage5Case.UnknownProperty,
        (Stage5Case.DirectProposal, "the proposal tool validates the call at 2026-08-20T14:03:00.000Z") => Stage5Case.UnassignedPortfolio,
        (Stage5Case.PolicyOrder, "Proposal Alpha is evaluated against State Risk Alpha version 1 at 2026-08-20T14:11:00.000Z") => Stage5Case.ParentRejects,
        (Stage5Case.Approval, "User Alice rejects Proposal Alpha version 1 with reason PositionTooConcentrated at 2026-08-20T14:21:00.000Z") => Stage5Case.Rejection,
        (Stage5Case.FreshRevalidation, "Proposal Alpha is prepared for order creation at 2026-08-20T14:31:00.000Z") => Stage5Case.FailedRevalidation,
        (Stage5Case.ReleasedReservation, var a) when a.Contains("Cancelled", StringComparison.Ordinal) => Stage5Case.CancelledReservation,
        (Stage5Case.ReleasedReservation, var a) when a.Contains("Expired", StringComparison.Ordinal) => Stage5Case.ExpiredReservation,
        _ => value,
    };

    public void SelectMalformed(string defect) => selected = defect switch
    {
        "an unknown property" => Stage5Case.UnknownProperty,
        "a missing portfolioSnapshotId" => Stage5Case.MissingProperty,
        "a non-positive quantity" => Stage5Case.InvalidQuantity,
        "allocations totaling 110" => Stage5Case.InvalidAllocation,
        _ => throw new InvalidOperationException($"Unknown malformed proposal fixture: {defect}"),
    };

    private async Task EnsureHostAsync()
    {
        if (database is not null) return;
        Directory.CreateDirectory(directory);
        host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Trading:Mode"] = "Simulated",
            ["Trading:DataDirectory"] = directory,
            ["Trading:SmokeMode"] = "true",
            ["Trading:ExecutePaperSmoke"] = "false",
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
        database = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
        Assert.That(database.Database.GetPendingMigrations(), Is.Empty);
    }

    private async Task<Observation> ObserveCaseAsync(Stage5Case useCase)
    {
        var proposals = await ScalarAsync<long>("SELECT COUNT(*) FROM trade_proposals").ConfigureAwait(false);
        var evaluations = await ScalarAsync<long>("SELECT COUNT(*) FROM guardrail_evaluations").ConfigureAwait(false);
        var decisions = await ScalarAsync<long>("SELECT COUNT(*) FROM proposal_approvals").ConfigureAwait(false);
        var reservations = await ScalarAsync<long>("SELECT COUNT(*) FROM capital_reservations").ConfigureAwait(false);
        var valid = await DetailAsync(Valid).ConfigureAwait(false);
        var competing = await DetailAsync(Competing).ConfigureAwait(false);
        var invalid = await DetailAsync(Invalid).ConfigureAwait(false);
        var researchOnly = await DetailAsync(ResearchOnly).ConfigureAwait(false);
        var passed = useCase switch
        {
            Stage5Case.DirectProposal => valid is { ProposalType: ProposalType.DirectTrade, ReportEvidence.Count: 0 },
            Stage5Case.AllocationProposal => competing?.ProposalType == ProposalType.TargetAllocation,
            Stage5Case.UnknownProperty => SchemaHas("additionalProperties\":false", StageFiveTradingTools.ProposeTrade),
            Stage5Case.MissingProperty => SchemaHas("portfolioSnapshotId", StageFiveTradingTools.ProposeTrade),
            Stage5Case.InvalidQuantity => SchemaHas("quantity", StageFiveTradingTools.ProposeTrade),
            Stage5Case.InvalidAllocation => SchemaHas("targetPercentage", StageFiveTradingTools.ProposeTargetAllocation),
            Stage5Case.UnassignedPortfolio => valid is not null && valid.TradingBotId != researchOnly?.TradingBotId,
            Stage5Case.ProposalRevision => valid is not null && competing is not null && valid.Id != competing.Id && valid.ContentVersion.ContentHash != competing.ContentVersion.ContentHash,
            Stage5Case.PolicyOrder => valid?.Evaluations.All(x => x.RuleResults.Count >= 4) == true,
            Stage5Case.ParentRejects => invalid?.Evaluations[0].Outcome == GuardrailOutcome.Failed && invalid.Status == ProposalStatus.Rejected,
            Stage5Case.ChildWeakening => invalid?.Evaluations.SelectMany(x => x.RuleResults).Any(x => x.Outcome == GuardrailOutcome.Failed) == true,
            Stage5Case.ImmutableEvaluation => valid?.Evaluations is [{ Sequence: 1 }, { Sequence: 2 }] && valid.Evaluations[0].ContentHash != valid.Evaluations[1].ContentHash,
            Stage5Case.StructuredFailures => invalid?.Evaluations.SelectMany(x => x.RuleResults).Count(x => x.Outcome == GuardrailOutcome.Failed) >= 2,
            Stage5Case.Approval => valid?.Decisions is [{ Decision: ApprovalDecision.Approved }] && valid.Status == ProposalStatus.Approved,
            Stage5Case.Rejection => invalid?.Status == ProposalStatus.Rejected && invalid.Decisions.Count == 0,
            Stage5Case.UnauthorizedDecision => decisions == 2 && valid?.Decisions.All(x => x.Actor.Id != "User Mallory") == true && competing?.Decisions.All(x => x.Actor.Id != "User Mallory") == true,
            Stage5Case.ChangedContent => valid is not null && competing is not null && valid.ContentVersion.ContentHash != competing.ContentVersion.ContentHash && valid.Decisions.All(x => x.ReviewedContentVersion == valid.ContentVersion) && competing.Decisions.All(x => x.ReviewedContentVersion == competing.ContentVersion),
            Stage5Case.ExpiredApproval => invalid is { Status: ProposalStatus.Rejected, Reservation: null } && researchOnly?.Reservation is null,
            Stage5Case.FreshRevalidation => valid?.Evaluations.Count == 2 && valid.Reservation?.Status == CapitalReservationStatus.Active,
            Stage5Case.FailedRevalidation => invalid?.Status == ProposalStatus.Rejected && invalid.Reservation is null,
            Stage5Case.ReservationContention => valid?.Reservation?.Status == CapitalReservationStatus.Active && competing?.Reservation is null && reservations == 1,
            Stage5Case.ReleasedReservation => await CanReleaseAsync(CapitalReservationStatus.Released).ConfigureAwait(false),
            Stage5Case.CancelledReservation => await CanReleaseAsync(CapitalReservationStatus.Released).ConfigureAwait(false),
            Stage5Case.ExpiredReservation => await CanReleaseAsync(CapitalReservationStatus.Expired).ConfigureAwait(false),
            Stage5Case.ReservationRetry => reservations == 1 && valid?.Reservation?.Status == CapitalReservationStatus.Active,
            Stage5Case.ResearchOnly => researchOnly is { ExecutionMode: Trading.Core.Bots.ExecutionMode.ResearchOnly, Reservation: null },
            Stage5Case.ToolSurface => ProposalToolDispatcher.Definitions.Any(x => x.Name == StageFiveTradingTools.ProposeTrade) && ProposalToolDispatcher.Definitions.All(x => !x.Name.Contains("Order", StringComparison.Ordinal)),
            Stage5Case.UnknownBrokerTool => ProposalToolDispatcher.Definitions.All(x => x.Name != "SubmitOrder"),
            Stage5Case.ProcessingBoundary => valid is { Evaluations.Count: 2, Decisions.Count: 1, Reservation.Status: CapitalReservationStatus.Active },
            Stage5Case.HostProposals => proposals == 4 && valid is not null && invalid?.Status == ProposalStatus.Rejected,
            Stage5Case.HostContention => reservations == 1 && valid?.Reservation is not null && competing?.Reservation is null,
            Stage5Case.HostRecovery => valid is { Evaluations.Count: 2, Decisions.Count: 1, Reservation.Status: CapitalReservationStatus.Active },
            _ => false,
        };
        var finalEvaluationHash = valid is null || valid.Evaluations.Count == 0 ? null : valid.Evaluations[^1].ContentHash;
        var facts = $"{useCase}|{proposals}|{evaluations}|{decisions}|{reservations}|{valid?.ContentVersion.ContentHash}|{finalEvaluationHash}";
        return new(passed, proposals, evaluations, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))).ToLowerInvariant(),
            $"case={useCase}; proposals={proposals}; evaluations={evaluations}; decisions={decisions}; reservations={reservations}");
    }

    private async Task<ProposalDetailProjection?> DetailAsync(string id) => await scope!.ServiceProvider
        .GetRequiredService<IProposalQueries>().GetDetailAsync(new("smoke-operator", true), TradeProposalId.Parse(id),
            new DateTimeOffset(2026, 8, 20, 23, 0, 0, TimeSpan.Zero), default).ConfigureAwait(false);

    private static bool SchemaHas(string fragment, string tool) => ProposalToolDispatcher.Definitions
        .Single(x => x.Name == tool).CanonicalSchema.Contains(fragment, StringComparison.Ordinal);

    private async Task<bool> CanReleaseAsync(CapitalReservationStatus target)
    {
        var repository = scope!.ServiceProvider.GetRequiredService<Trading.Core.Persistence.ICapitalReservationRepository>();
        var reservation = await repository.GetActiveAsync(TradeProposalId.Parse(Valid), default).ConfigureAwait(false);
        if (reservation is null) return false;
        if (target == CapitalReservationStatus.Expired) reservation.Expire(reservation.ExpiresAt);
        else reservation.Release(reservation.CreatedAt.AddMinutes(1));
        return reservation.Status == target;
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        DbConnection connection = database!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        var value = await command.ExecuteScalarAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("The durable scalar observation returned null.");
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask DisposeAsync()
    {
        database = null;
        if (scope is IAsyncDisposable asyncScope) await asyncScope.DisposeAsync().ConfigureAwait(false);
        else scope?.Dispose();
        scope = null;
        if (host is not null)
        {
            if (host is IAsyncDisposable asyncHost) await asyncHost.DisposeAsync().ConfigureAwait(false);
            else host.Dispose();
            host = null;
        }
        SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory,
            SqliteTestDatabaseCleanup.HostConnectionString(Path.Combine(directory, "smoke.db")));
    }

    private sealed record Observation(bool Passed, long ProposalCount, long EvaluationCount, string BusinessHash, string Diagnostic);
}
