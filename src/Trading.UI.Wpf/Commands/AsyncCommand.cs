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
        (allowConcurrentExecutions || !executing) && parameter is T value && (canExecute?.Invoke(value) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter) || parameter is not T value) return;
        executing = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(value, CancellationToken.None); }
        finally
        {
            executing = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
