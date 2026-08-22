using Microsoft.EntityFrameworkCore;
using Trading.Core.Operations;

namespace Trading.Data;

public sealed class KillSwitchStore(TradingDbContext db) : IKillSwitchStore
{
    public async Task<KillSwitchChangeResult> ChangeAsync(KillSwitchChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (string.IsNullOrWhiteSpace(change.IdempotencyKey) || string.IsNullOrWhiteSpace(change.Reason) ||
            string.IsNullOrWhiteSpace(change.ActorId) || string.IsNullOrWhiteSpace(change.Confirmation) ||
            change.ChangedAt.Offset != TimeSpan.Zero || change.ExpectedVersion < 0)
            return new(KillSwitchChangeStatus.Invalid, null, KillSwitchReasonCodes.Invalid);

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var duplicate = await db.KillSwitchHistory.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == change.IdempotencyKey, cancellationToken);
        if (duplicate is not null)
        {
            var exact = duplicate.ScopeKind == change.Scope.Kind.ToString() && duplicate.ScopeId == change.Scope.Id &&
                duplicate.ResultingState == change.State.ToString() && duplicate.Reason == change.Reason.Trim() &&
                duplicate.ActorId == change.ActorId.Trim() && duplicate.Confirmation == change.Confirmation.Trim() &&
                duplicate.Version == change.ExpectedVersion + 1;
            await transaction.CommitAsync(cancellationToken);
            return exact
                ? new(KillSwitchChangeStatus.Idempotent, ToSnapshot(duplicate), "operations.kill_switch.idempotent")
                : new(KillSwitchChangeStatus.Conflict, null, KillSwitchReasonCodes.Conflict);
        }

        var kind = change.Scope.Kind.ToString();
        var current = await db.KillSwitches.SingleOrDefaultAsync(x => x.ScopeKind == kind && x.ScopeId == change.Scope.Id, cancellationToken);
        var priorState = current?.State ?? KillSwitchState.Clear.ToString();
        var priorVersion = current?.Version ?? 0;
        if (priorVersion != change.ExpectedVersion)
            return new(KillSwitchChangeStatus.Conflict, current is null ? null : ToSnapshot(current), KillSwitchReasonCodes.Conflict);

        var nextVersion = priorVersion + 1;
        if (current is null)
        {
            current = new KillSwitchEntity { ScopeKind = kind, ScopeId = change.Scope.Id };
            db.KillSwitches.Add(current);
        }
        current.State = change.State.ToString();
        current.Reason = change.Reason.Trim();
        current.ActorId = change.ActorId.Trim();
        current.Confirmation = change.Confirmation.Trim();
        current.ChangedAt = UtcUnixMilliseconds.ToProvider(change.ChangedAt);
        current.Version = nextVersion;
        db.KillSwitchHistory.Add(new KillSwitchHistoryEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            IdempotencyKey = change.IdempotencyKey.Trim(),
            ScopeKind = kind,
            ScopeId = change.Scope.Id,
            PriorState = priorState,
            ResultingState = current.State,
            Reason = current.Reason,
            ActorId = current.ActorId,
            Confirmation = current.Confirmation,
            ChangedAt = current.ChangedAt,
            Version = nextVersion,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(KillSwitchChangeStatus.Applied, ToSnapshot(current), "operations.kill_switch.changed");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(KillSwitchChangeStatus.Conflict, null, KillSwitchReasonCodes.Conflict);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new(KillSwitchChangeStatus.Conflict, null, KillSwitchReasonCodes.Conflict);
        }
    }

    public async Task<KillSwitchSnapshot?> GetAsync(KillSwitchScope scope, CancellationToken cancellationToken)
    {
        var kind = scope.Kind.ToString();
        var row = await db.KillSwitches.AsNoTracking().SingleOrDefaultAsync(x => x.ScopeKind == kind && x.ScopeId == scope.Id, cancellationToken);
        return row is null ? null : ToSnapshot(row);
    }

    public async Task<IReadOnlyList<KillSwitchHistoryEntry>> GetHistoryAsync(KillSwitchScope scope, CancellationToken cancellationToken)
    {
        var kind = scope.Kind.ToString();
        var rows = await db.KillSwitchHistory.AsNoTracking().Where(x => x.ScopeKind == kind && x.ScopeId == scope.Id)
            .OrderBy(x => x.Version).ToListAsync(cancellationToken);
        return rows.Select(x => new KillSwitchHistoryEntry(x.Id, scope, ParseState(x.PriorState), ParseState(x.ResultingState),
            x.Reason, x.ActorId, x.Confirmation, UtcUnixMilliseconds.FromProvider(x.ChangedAt), x.Version)).ToArray();
    }

    public async Task<EffectiveKillSwitch> GetEffectiveAsync(KillSwitchHierarchy hierarchy, CancellationToken cancellationToken)
    {
        var scopes = new List<KillSwitchScope> { KillSwitchScope.Platform };
        if (hierarchy.BrokerAccountId is not null) scopes.Add(new(KillSwitchScopeKind.BrokerAccount, hierarchy.BrokerAccountId));
        if (hierarchy.PortfolioId is not null) scopes.Add(new(KillSwitchScopeKind.Portfolio, hierarchy.PortfolioId));
        if (hierarchy.TradingBotId is not null) scopes.Add(new(KillSwitchScopeKind.TradingBot, hierarchy.TradingBotId));
        foreach (var scope in scopes)
        {
            var snapshot = await GetAsync(scope, cancellationToken);
            if (snapshot?.State == KillSwitchState.Active)
                return new(true, KillSwitchReasonCodes.Blocked, snapshot);
        }
        return new(false, string.Empty, null);
    }

    private static KillSwitchSnapshot ToSnapshot(KillSwitchEntity x) => new(new(ParseKind(x.ScopeKind), x.ScopeId),
        ParseState(x.State), x.Reason, x.ActorId, x.Confirmation, UtcUnixMilliseconds.FromProvider(x.ChangedAt), x.Version);
    private static KillSwitchSnapshot ToSnapshot(KillSwitchHistoryEntity x) => new(new(ParseKind(x.ScopeKind), x.ScopeId),
        ParseState(x.ResultingState), x.Reason, x.ActorId, x.Confirmation, UtcUnixMilliseconds.FromProvider(x.ChangedAt), x.Version);
    private static KillSwitchScopeKind ParseKind(string value) => Enum.Parse<KillSwitchScopeKind>(value, false);
    private static KillSwitchState ParseState(string value) => Enum.Parse<KillSwitchState>(value, false);
}
