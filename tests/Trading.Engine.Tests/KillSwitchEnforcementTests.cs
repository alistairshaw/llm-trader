using NUnit.Framework;
using Trading.Core.Operations;
using Trading.Engine.Operations;

namespace Trading.Engine.Tests;

[TestFixture, Category("KillSwitch")]
internal sealed class KillSwitchEnforcementTests
{
    [TestCase(KillSwitchCheckpoint.RunAdmission)]
    [TestCase(KillSwitchCheckpoint.ProposalDecision)]
    [TestCase(KillSwitchCheckpoint.CapitalReservation)]
    [TestCase(KillSwitchCheckpoint.OrderConversion)]
    [TestCase(KillSwitchCheckpoint.BrokerSubmission)]
    public async Task ActiveAncestorBlocksEveryCoveredCheckpoint(KillSwitchCheckpoint checkpoint)
    {
        var source = new KillSwitchSnapshot(KillSwitchScope.Platform, KillSwitchState.Active, "emergency",
            "operator", "CONFIRM", new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero), 1);
        var enforcement = new KillSwitchEnforcement(new StubStore(new(true, KillSwitchReasonCodes.Blocked, source)));

        var result = await enforcement.CheckAsync(checkpoint, new("account", "portfolio", "bot"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Allowed, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo(KillSwitchReasonCodes.Blocked));
            Assert.That(result.BlockingSwitch, Is.SameAs(source));
        });
    }

    private sealed class StubStore(EffectiveKillSwitch result) : IKillSwitchStore
    {
        public Task<EffectiveKillSwitch> GetEffectiveAsync(KillSwitchHierarchy hierarchy, CancellationToken cancellationToken) => Task.FromResult(result);
        public Task<KillSwitchChangeResult> ChangeAsync(KillSwitchChange change, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<KillSwitchSnapshot?> GetAsync(KillSwitchScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<KillSwitchHistoryEntry>> GetHistoryAsync(KillSwitchScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
