using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Trading.Host;

namespace Trading.UI.Wpf;

public partial class App : Application, IAsyncDisposable
{
    private TradingApplicationLifecycle? lifecycle;
    private bool closing;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlmTrader");
        var host = HostBootstrap.Build(e.Args, builder => builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Trading:Mode"] = "Simulated",
                ["Trading:DataDirectory"] = dataDirectory,
                ["Trading:OperatorMode"] = "true",
                ["Research:Mode"] = "Fixture",
            }));
        lifecycle = new(host, TimeSpan.FromSeconds(30));
        try
        {
            await lifecycle.StartAsync(CancellationToken.None);
            var window = new MainWindow();
            window.Closing += OnMainWindowClosing;
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Trading Bot could not start.\n\n{exception.Message}", "Startup failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        closing = true;
        if (lifecycle is not null) await lifecycle.StopAsync();
        if (sender is Window window)
        {
            window.Closing -= OnMainWindowClosing;
            window.Close();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        lifecycle?.StopAsync().GetAwaiter().GetResult();
        base.OnExit(e);
    }

    public async ValueTask DisposeAsync()
    {
        if (lifecycle is not null) await lifecycle.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
