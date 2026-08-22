using NUnit.Framework;
using Trading.UI.Wpf.Services;

namespace Trading.IntegrationTests;

[TestFixture]
[Category("OperatorUpdates")]
public sealed class OperatorUpdatesIntegrationTests
{
    [Test]
    public async Task CoalescesIdentityInSequenceAndPreservesTerminalTransition()
    {
        await using var source = new BoundedOperatorUpdateBuffer(3);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var enumerator = source.SubscribeAsync(
            new HashSet<OperatorUpdateKind> { OperatorUpdateKind.Orders }, cancellation.Token).GetAsyncEnumerator();

        await source.PublishAsync(new(OperatorUpdateKind.Orders, "order-1", 1));
        await source.PublishAsync(new(OperatorUpdateKind.Orders, "order-1", 2, true));
        await source.PublishAsync(new(OperatorUpdateKind.Orders, "order-1", 3));

        Assert.That(await enumerator.MoveNextAsync(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(enumerator.Current.Sequence, Is.EqualTo(2));
            Assert.That(enumerator.Current.IsTerminal, Is.True);
        });
    }

    [Test]
    public async Task BoundedPublisherIsCancellationSafeDuringBurst()
    {
        await using var source = new BoundedOperatorUpdateBuffer(1);
        using var subscriptionCancellation = new CancellationTokenSource();
        await using var enumerator = source.SubscribeAsync(Enum.GetValues<OperatorUpdateKind>().ToHashSet(),
            subscriptionCancellation.Token).GetAsyncEnumerator();
        await source.PublishAsync(new(OperatorUpdateKind.Bots, "bot-1", 1));

        using var publishCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        Assert.That(async () => await source.PublishAsync(new(OperatorUpdateKind.Runs, "run-1", 1),
            publishCancellation.Token), Throws.InstanceOf<OperationCanceledException>());

        await subscriptionCancellation.CancelAsync();
    }

    [Test]
    public void RejectsUnboundedOrInvalidIdentity()
    {
        var source = new BoundedOperatorUpdateBuffer();
        Assert.That(async () => await source.PublishAsync(new(OperatorUpdateKind.Warnings,
            new string('x', OperatorUpdate.MaximumIdentityLength + 1), 1)), Throws.InstanceOf<ArgumentException>());
    }
}
