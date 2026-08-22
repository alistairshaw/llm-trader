using System.Windows.Threading;

namespace Trading.UI.Wpf.Services;

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public async Task InvokeAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await dispatcher.InvokeAsync(action, DispatcherPriority.DataBind, cancellationToken).Task.Unwrap();
    }
}
