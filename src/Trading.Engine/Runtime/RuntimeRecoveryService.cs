using Trading.Core.Bots;
using Trading.Core.Persistence;

namespace Trading.Engine.Runtime;

public sealed class RuntimeRecoveryService(
    IBotRunRepository runs,
    IRuntimeIdentifierGenerator identifiers,
    IUtcClock clock)
{
    public async Task<RecoveryResult> RecoverExpiredLeasesAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (now.Offset != TimeSpan.Zero) throw new InvalidOperationException("The runtime clock must return UTC.");
        var recovered = 0;
        var faulted = 0;
        foreach (var id in await runs.GetExpiredLeaseRunIdsAsync(now, cancellationToken).ConfigureAwait(false))
        {
            var run = await runs.GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (run is null || run.IsTerminal || run.LeaseExpiresAt is null || run.LeaseExpiresAt > now) continue;
            var preModel = run.Status == BotRunStatus.PreparingSnapshot;
            foreach (var tool in run.ToolInvocations.Where(x => x.Status == ToolInvocationStatus.Running))
                tool.Fail("recovery_interrupted_tool", run.Usage, now);
            run.RecordTerminalReason(preModel ? "recovery_pre_model_checkpoint" : "recovery_model_execution_interrupted");
            run.Fault(run.Usage, now);
            var followUp = preModel ? new PendingBotRunTrigger(identifiers.NewTriggerId(), run.TradingBotId,
                BotRunTriggerType.RiskOrReconciliation, "Resume after expired pre-model lease", now, now,
                "runtime-recovery", run.Id.ToString()) : null;
            if (await runs.RecoverExpiredAsync(run, run.Version, followUp, cancellationToken).ConfigureAwait(false)
                is PersistenceWriteResult.Succeeded)
            {
                if (preModel)
                {
                    recovered++;
                }
                else faulted++;
            }
        }
        return new(recovered, faulted);
    }
}
