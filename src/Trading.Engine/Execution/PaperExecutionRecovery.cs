using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;

namespace Trading.Engine.Execution;

public interface IPaperBrokerAccountReconciler
{
    Task<bool> ReconcileAsync(BrokerAccountId accountId, CancellationToken cancellationToken);
}

public interface IPaperExecutionRecoveryObserver
{
    Task CheckpointAsync(string checkpoint, CancellationToken cancellationToken);
}

public static class PaperExecutionRecoveryCheckpoints
{
    public const string BeforeDurableRecovery = "paper_execution.recovery.before_durable_recovery";
    public const string AfterDurableRecovery = "paper_execution.recovery.after_durable_recovery";
    public const string BeforeAccountReconciliation = "paper_execution.recovery.before_account_reconciliation";
    public const string AfterAccountReconciliation = "paper_execution.recovery.after_account_reconciliation";
    public const string BeforeOutboxDrain = "paper_execution.recovery.before_outbox_drain";
    public const string AfterOutboxDrain = "paper_execution.recovery.after_outbox_drain";
    public const string BeforeInboxDrain = "paper_execution.recovery.before_inbox_drain";
    public const string AfterInboxDrain = "paper_execution.recovery.after_inbox_drain";
}

public sealed record PaperExecutionRecoveryOptions(int MaximumRecoveredSubmissions, int MaximumDrainCycles)
{
    public static PaperExecutionRecoveryOptions Default { get; } = new(128, 64);

    public PaperExecutionRecoveryOptions Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumRecoveredSubmissions, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumDrainCycles, 1);
        return this;
    }
}

public sealed record PaperExecutionStartupResult(
    PaperExecutionRecoveryResult Recovery,
    int ReconciledAccounts,
    int OutboxProcessed,
    int InboxProcessed,
    bool IsReady);

public sealed class PaperExecutionRecoveryService(
    IPaperExecutionRecoveryRepository recovery,
    IBrokerReconciliationRepository audit,
    IPaperBrokerAccountReconciler accounts,
    OrderOutboxProcessor outbox,
    BrokerInboxProcessor inbox,
    IOrderExecutionClock clock,
    IOrderExecutionIdentifierSource identifiers,
    PaperExecutionRecoveryOptions options,
    IPaperExecutionRecoveryObserver? observer = null)
{
    private readonly PaperExecutionRecoveryOptions options = options.Validate();

    public async Task<PaperExecutionStartupResult> RecoverAndDrainAsync(CancellationToken cancellationToken)
    {
        var at = clock.UtcNow;
        await Checkpoint(PaperExecutionRecoveryCheckpoints.BeforeDurableRecovery, cancellationToken).ConfigureAwait(false);
        var recovered = await recovery.RecoverAsync(new(at,
            Enumerable.Range(0, checked(options.MaximumRecoveredSubmissions * 2))
                .Select(_ => identifiers.NewTransitionId()).ToArray(),
            Enumerable.Range(0, options.MaximumRecoveredSubmissions)
                .Select(_ => identifiers.NewWorkItemId()).ToArray()), cancellationToken).ConfigureAwait(false);
        await Checkpoint(PaperExecutionRecoveryCheckpoints.AfterDurableRecovery, cancellationToken).ConfigureAwait(false);

        var accountIds = recovered.Scopes.Select(x => x.BrokerAccountId).Distinct()
            .OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
        var reconciledAccounts = 0;
        foreach (var accountId in accountIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = clock.UtcNow;
            await Checkpoint(PaperExecutionRecoveryCheckpoints.BeforeAccountReconciliation, cancellationToken).ConfigureAwait(false);
            var reconciled = await accounts.ReconcileAsync(accountId, cancellationToken).ConfigureAwait(false);
            await Checkpoint(PaperExecutionRecoveryCheckpoints.AfterAccountReconciliation, cancellationToken).ConfigureAwait(false);
            var completedAt = clock.UtcNow;
            await AppendAuditAsync(accountId, recovered, reconciled, startedAt, completedAt,
                cancellationToken).ConfigureAwait(false);
            if (!reconciled)
                return new(recovered, reconciledAccounts, 0, 0, false);
            reconciledAccounts++;
        }

        var outboxProcessed = 0;
        var inboxProcessed = 0;
        for (var cycle = 0; cycle < options.MaximumDrainCycles; cycle++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Reconciliation/submission work is always settled before deferred broker events and fills.
            await Checkpoint(PaperExecutionRecoveryCheckpoints.BeforeOutboxDrain, cancellationToken).ConfigureAwait(false);
            var outboxResult = await outbox.DrainOnceAsync(cancellationToken).ConfigureAwait(false);
            await Checkpoint(PaperExecutionRecoveryCheckpoints.AfterOutboxDrain, cancellationToken).ConfigureAwait(false);
            await Checkpoint(PaperExecutionRecoveryCheckpoints.BeforeInboxDrain, cancellationToken).ConfigureAwait(false);
            var inboxResult = await inbox.DrainOnceAsync(cancellationToken).ConfigureAwait(false);
            await Checkpoint(PaperExecutionRecoveryCheckpoints.AfterInboxDrain, cancellationToken).ConfigureAwait(false);
            outboxProcessed += outboxResult.Processed;
            inboxProcessed += inboxResult.Processed;
            if (outboxResult.Claimed == 0 && inboxResult.Claimed == 0)
                return new(recovered, reconciledAccounts, outboxProcessed, inboxProcessed, true);
        }
        return new(recovered, reconciledAccounts, outboxProcessed, inboxProcessed, false);
    }

    private Task Checkpoint(string checkpoint, CancellationToken token) => observer?.CheckpointAsync(checkpoint, token) ?? Task.CompletedTask;

    private async Task AppendAuditAsync(BrokerAccountId accountId, PaperExecutionRecoveryResult recovered,
        bool reconciled, DateTimeOffset startedAt, DateTimeOffset completedAt, CancellationToken token)
    {
        var accountScopes = recovered.Scopes.Where(x => x.BrokerAccountId == accountId)
            .OrderBy(x => x.PortfolioId.ToString(), StringComparer.Ordinal)
            .ThenBy(x => x.OrderId.ToString(), StringComparer.Ordinal)
            .Select(x => new { portfolioId = x.PortfolioId.ToString(), orderId = x.OrderId.ToString() }).ToArray();
        var snapshot = JsonSerializer.Serialize(new
        {
            accountId = accountId.ToString(),
            scopes = accountScopes,
            submissionClaimsConverted = recovered.SubmissionClaimsConverted,
            outboxClaimsReleased = recovered.OutboxClaimsReleased,
            inboxClaimsReleased = recovered.InboxClaimsReleased,
            failedOutboxItems = recovered.FailedOutboxItems,
            failedInboxItems = recovered.FailedInboxItems
        });
        var resolution = JsonSerializer.Serialize(new
        {
            code = reconciled ? "paper_execution.recovery.account_reconciled" : "paper_execution.recovery.account_failed"
        });
        var correlation = identifiers.NewCorrelationId();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot + "\n" + resolution)))
            .ToLowerInvariant();
        var result = await audit.AppendAsync(new(correlation.Value, accountId,
            reconciled ? "Matched" : "Failed", startedAt, completedAt, snapshot, "{}", resolution,
            correlation, hash), token).ConfigureAwait(false);
        if (result is not PersistenceWriteResult.Succeeded and not PersistenceWriteResult.UniquenessConflict)
            throw new InvalidOperationException("Paper execution recovery audit could not be persisted.");
    }
}
