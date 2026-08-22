using Trading.Core.Operations;

namespace Trading.Engine.Operations;

public enum KillSwitchCheckpoint { RunAdmission, ProposalDecision, CapitalReservation, OrderConversion, BrokerSubmission }
public sealed record KillSwitchAdmission(bool Allowed, string ReasonCode, KillSwitchCheckpoint Checkpoint,
    KillSwitchSnapshot? BlockingSwitch);

public sealed class KillSwitchEnforcement(IKillSwitchStore store)
{
    public async Task<KillSwitchAdmission> CheckAsync(KillSwitchCheckpoint checkpoint, KillSwitchHierarchy hierarchy,
        CancellationToken cancellationToken)
    {
        var effective = await store.GetEffectiveAsync(hierarchy, cancellationToken).ConfigureAwait(false);
        return effective.IsBlocked
            ? new(false, KillSwitchReasonCodes.Blocked, checkpoint, effective.Source)
            : new(true, string.Empty, checkpoint, null);
    }
}
