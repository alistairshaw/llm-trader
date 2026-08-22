using System.Diagnostics;

namespace Trading.UI.Wpf.AcceptanceTests.Infrastructure;

internal static class BoundedWait
{
    public static async Task UntilAsync(Func<bool> condition, TimeSpan timeout, string description,
        CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition()) return;
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0} seconds waiting for {description}.");
    }
}
