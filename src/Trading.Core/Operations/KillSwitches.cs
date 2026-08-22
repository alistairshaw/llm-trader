namespace Trading.Core.Operations;

public enum KillSwitchScopeKind { Platform, BrokerAccount, Portfolio, TradingBot }
public enum KillSwitchState { Clear, Active }
public enum KillSwitchChangeStatus { Applied, Idempotent, Conflict, Invalid }

public sealed record KillSwitchScope
{
    public KillSwitchScope(KillSwitchScopeKind kind, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (kind == KillSwitchScopeKind.Platform && !string.Equals(id, "platform", StringComparison.Ordinal))
            throw new ArgumentException("The platform scope ID must be 'platform'.", nameof(id));
        Kind = kind;
        Id = id.Trim();
    }
    public KillSwitchScopeKind Kind { get; }
    public string Id { get; }
    public static KillSwitchScope Platform { get; } = new(KillSwitchScopeKind.Platform, "platform");
}

public sealed record KillSwitchSnapshot(KillSwitchScope Scope, KillSwitchState State, string Reason, string ActorId,
    string Confirmation, DateTimeOffset ChangedAt, long Version);
public sealed record KillSwitchHistoryEntry(string Id, KillSwitchScope Scope, KillSwitchState PriorState,
    KillSwitchState ResultingState, string Reason, string ActorId, string Confirmation, DateTimeOffset ChangedAt,
    long Version);
public sealed record KillSwitchChange(string IdempotencyKey, KillSwitchScope Scope, KillSwitchState State,
    long ExpectedVersion, string Reason, string ActorId, string Confirmation, DateTimeOffset ChangedAt);
public sealed record KillSwitchChangeResult(KillSwitchChangeStatus Status, KillSwitchSnapshot? Snapshot,
    string ReasonCode);
public sealed record KillSwitchHierarchy(string? BrokerAccountId, string? PortfolioId, string? TradingBotId);
public sealed record EffectiveKillSwitch(bool IsBlocked, string ReasonCode, KillSwitchSnapshot? Source);

public interface IKillSwitchStore
{
    Task<KillSwitchChangeResult> ChangeAsync(KillSwitchChange change, CancellationToken cancellationToken);
    Task<KillSwitchSnapshot?> GetAsync(KillSwitchScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyList<KillSwitchHistoryEntry>> GetHistoryAsync(KillSwitchScope scope, CancellationToken cancellationToken);
    Task<EffectiveKillSwitch> GetEffectiveAsync(KillSwitchHierarchy hierarchy, CancellationToken cancellationToken);
}

public static class KillSwitchReasonCodes
{
    public const string Blocked = "operations.kill_switch.active";
    public const string Conflict = "operations.kill_switch.version_conflict";
    public const string Invalid = "operations.kill_switch.invalid";
}
