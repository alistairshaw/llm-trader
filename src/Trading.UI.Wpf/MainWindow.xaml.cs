using System.Windows;
using Trading.UI.Wpf.Navigation;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellViewModel(new PlaceholderNavigationPageFactory());
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell) await shell.NavigateAsync(shell.Routes[0]);
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is IAsyncDisposable disposable) await disposable.DisposeAsync();
    }
}
