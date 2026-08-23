using System.Windows.Input;

namespace Trading.UI.Wpf.Commands;

public sealed class AsyncCommand<T>(
    Func<T, CancellationToken, Task> execute,
    Func<T, bool>? canExecute = null,
    bool allowConcurrentExecutions = false) : ICommand
{
    private bool executing;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        (allowConcurrentExecutions || !executing) && TryParameter(parameter, out var value) &&
        (canExecute?.Invoke(value) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter) || !TryParameter(parameter, out var value)) return;
        executing = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(value, CancellationToken.None); }
        finally
        {
            executing = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static bool TryParameter(object? parameter, out T value)
    {
        if (parameter is T typed) { value = typed; return true; }
        if (parameter is null && default(T) is null) { value = default!; return true; }
        value = default!;
        return false;
    }
}
