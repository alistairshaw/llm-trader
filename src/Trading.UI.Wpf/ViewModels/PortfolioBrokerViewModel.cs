using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Trading.Core.Persistence;
using Trading.UI.Wpf.Commands;

namespace Trading.UI.Wpf.ViewModels;

public enum PortfolioBrokerLoadStatus { Succeeded, Denied }

public sealed record PortfolioBrokerLoadResult(PortfolioBrokerLoadStatus Status,
    IReadOnlyList<OperatorPortfolioBrokerView> Items);

public interface IPortfolioBrokerViewSource
{
    Task<PortfolioBrokerLoadResult> LoadAsync(string? search, string? status, int offset, int size,
        CancellationToken cancellationToken);
}

public sealed record PortfolioBrokerRowViewModel(
    string PortfolioId,
    string PortfolioName,
    string Capital,
    string Positions,
    string Ledger,
    string Account,
    string Connection,
    string Environment,
    string Capabilities,
    string Mappings,
    string Reconciliation,
    string Updated,
    bool IsDisconnected,
    bool IsUncertain,
    bool IsStale,
    string AutomationStatus);

public sealed class PortfolioBrokerViewModel : ObservableViewModel, IAsyncDisposable
{
    public const int PageSize = 25;
    private readonly IPortfolioBrokerViewSource source;
    private readonly TimeProvider timeProvider;
    private CancellationTokenSource? loadCancellation;
    private string? search;
    private string? statusFilter;
    private string stateText = "Portfolio and broker data has not been loaded.";
    private string safetyAnnouncement = "No broker status available.";
    private bool isLoading;
    private bool isDenied;
    private int offset;

    public PortfolioBrokerViewModel(IPortfolioBrokerViewSource source, TimeProvider? timeProvider = null)
    {
        this.source = source;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        RefreshCommand = new AsyncCommand<object?>((_, token) => RefreshAsync(token));
        NextPageCommand = new AsyncCommand<object?>((_, token) => NextPageAsync(token));
        PreviousPageCommand = new AsyncCommand<object?>((_, token) => PreviousPageAsync(token));
    }

    public ObservableCollection<PortfolioBrokerRowViewModel> Items { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public string? Search { get => search; set => SetProperty(ref search, value); }
    public string? StatusFilter { get => statusFilter; set => SetProperty(ref statusFilter, value); }
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
    public bool IsDenied { get => isDenied; private set => SetProperty(ref isDenied, value); }
    public bool IsEmpty => !IsLoading && !IsDenied && Items.Count == 0;
    public int PageNumber => (offset / PageSize) + 1;
    public string StateText { get => stateText; private set => SetProperty(ref stateText, value); }
    public string SafetyAnnouncement { get => safetyAnnouncement; private set => SetProperty(ref safetyAnnouncement, value); }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var next = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previous = Interlocked.Exchange(ref loadCancellation, next);
        if (previous is not null) { await previous.CancelAsync(); previous.Dispose(); }
        IsLoading = true;
        IsDenied = false;
        StateText = "Loading authorized portfolio and broker status.";
        OnPropertyChanged(nameof(IsEmpty));
        try
        {
            var result = await source.LoadAsync(Search?.Trim(), StatusFilter?.Trim(), offset, PageSize, next.Token);
            next.Token.ThrowIfCancellationRequested();
            Items.Clear();
            if (result.Status == PortfolioBrokerLoadStatus.Denied)
            {
                IsDenied = true;
                StateText = "Portfolio and broker status is unavailable for this operator.";
                SafetyAnnouncement = "Access denied. No portfolio or broker facts are displayed.";
                return;
            }
            foreach (var item in result.Items) Items.Add(Map(item));
            StateText = Items.Count == 0 ? "No authorized portfolios match the current filters." :
                $"Showing {Items.Count} authorized portfolio{(Items.Count == 1 ? string.Empty : "s")} on page {PageNumber}.";
            var unsafeCount = Items.Count(x => x.IsDisconnected || x.IsUncertain || x.IsStale);
            SafetyAnnouncement = unsafeCount == 0 ? "All displayed broker states are current and connected." :
                $"Warning: {unsafeCount} displayed portfolio{(unsafeCount == 1 ? string.Empty : "s")} require operator attention.";
        }
        finally
        {
            if (ReferenceEquals(loadCancellation, next)) IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(PageNumber));
        }
    }

    public async Task NextPageAsync(CancellationToken token = default) { offset += PageSize; await RefreshAsync(token); }
    public async Task PreviousPageAsync(CancellationToken token = default) { offset = Math.Max(0, offset - PageSize); await RefreshAsync(token); }

    private PortfolioBrokerRowViewModel Map(OperatorPortfolioBrokerView item)
    {
        var disconnected = !string.Equals(item.ConnectionStatus, "Enabled", StringComparison.OrdinalIgnoreCase);
        var uncertain = item.ReconciliationStatus.Contains("uncertain", StringComparison.OrdinalIgnoreCase) ||
            item.ReconciliationStatus.Contains("pending", StringComparison.OrdinalIgnoreCase);
        var stale = timeProvider.GetUtcNow() - item.UpdatedAt > TimeSpan.FromMinutes(15);
        var environment = string.Equals(item.Environment, "Paper", StringComparison.OrdinalIgnoreCase)
            ? "PAPER — simulated broker environment" : item.Environment.ToUpperInvariant();
        var flags = new List<string>();
        if (disconnected) flags.Add("DISCONNECTED");
        if (uncertain) flags.Add("RECONCILIATION UNCERTAIN");
        if (stale) flags.Add("STALE DATA");
        if (flags.Count == 0) flags.Add("CONNECTED AND CURRENT");
        return new(item.PortfolioId.ToString(), item.PortfolioName,
            Money(item.CapitalAllocation, item.Currency),
            $"{item.PositionCount.ToString(CultureInfo.InvariantCulture)} positions; {Decimal(item.PositionQuantity)} total units",
            Money(item.LedgerTotal, item.Currency), $"{item.AccountName} ({item.AccountStatus})",
            $"{item.ConnectionName} ({item.ConnectionStatus})", environment,
            item.Capabilities.Count == 0 ? "None reported" : string.Join(", ", item.Capabilities),
            $"{item.MappingCount.ToString(CultureInfo.InvariantCulture)} active mappings", item.ReconciliationStatus,
            item.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture), disconnected, uncertain, stale,
            $"{string.Join("; ", flags)}; {environment}; reconciliation {item.ReconciliationStatus}");
    }

    private static string Decimal(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private static string Money(decimal value, string currency) => $"{Decimal(value)} {currency.ToUpperInvariant()}";

    public async ValueTask DisposeAsync()
    {
        var cancellation = Interlocked.Exchange(ref loadCancellation, null);
        if (cancellation is not null) { await cancellation.CancelAsync(); cancellation.Dispose(); }
    }
}
