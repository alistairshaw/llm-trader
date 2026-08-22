using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.UI.Wpf.Commands;

namespace Trading.UI.Wpf.ViewModels;

public sealed record ExecutionOrderRow(OrderId Id, string Identity, string Scope, string Order,
    string Status, string Updated, bool RequiresAttention, string AccessibleSummary);
public sealed record ExecutionFillRow(string Identity, string Quantity, string Price, string Gross,
    string Fee, string Executed);
public sealed record ExecutionEffectRow(string Kind, string Identity, string Effect, string Source, string At);
public sealed record ExecutionAuditRow(string Kind, string Identity, string Status, string Code,
    string Correlation, string At, string Summary);

public sealed class ExecutionRiskAuditViewModel : ObservableViewModel, IAsyncDisposable
{
    public const int PageSize = 25;
    private readonly IOrderExecutionQueries queries;
    private readonly ExecutionQueryPrincipal principal;
    private readonly CancellationTokenSource lifetime = new();
    private OrderListItem? selectedOrder;
    private string? search;
    private string? riskFilter;
    private string stateText = "Execution data has not been loaded.";
    private string riskAnnouncement = "No execution risk state available.";
    private bool isBusy;
    private int offset;

    public ExecutionRiskAuditViewModel(IOrderExecutionQueries queries, ExecutionQueryPrincipal principal)
    {
        this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
        this.principal = principal ?? throw new ArgumentNullException(nameof(principal));
        RefreshCommand = new AsyncCommand<object?>((_, token) => RefreshAsync(token));
        LoadDetailCommand = new AsyncCommand<object?>((_, token) => LoadDetailAsync(token));
        NextPageCommand = new AsyncCommand<object?>((_, token) => NextPageAsync(token));
        PreviousPageCommand = new AsyncCommand<object?>((_, token) => PreviousPageAsync(token));
    }

    public ObservableCollection<OrderListItem> Orders { get; } = [];
    public ObservableCollection<ExecutionOrderRow> Queue { get; } = [];
    public ObservableCollection<ExecutionFillRow> Fills { get; } = [];
    public ObservableCollection<ExecutionEffectRow> Effects { get; } = [];
    public ObservableCollection<ExecutionAuditRow> Audit { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand LoadDetailCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public string? Search { get => search; set => SetProperty(ref search, value); }
    public string? RiskFilter { get => riskFilter; set => SetProperty(ref riskFilter, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public int PageNumber => (offset / PageSize) + 1;
    public string StateText { get => stateText; private set => SetProperty(ref stateText, value); }
    public string RiskAnnouncement { get => riskAnnouncement; private set => SetProperty(ref riskAnnouncement, value); }
    public OrderListItem? SelectedOrder { get => selectedOrder; set => SetProperty(ref selectedOrder, value); }
    public string OrderFinancials { get; private set; } = "No Order selected.";
    public string Reservation { get; private set; } = string.Empty;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var status = ParseRiskFilter(RiskFilter);
        var rows = await queries.GetOrdersAsync(principal, new(Status: status), new(offset, PageSize), token);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            rows = rows.Where(x => x.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.ClientOrderId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.CorrelationId.Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        Orders.Clear(); Queue.Clear();
        foreach (var row in rows.GroupBy(x => x.Id).Select(x => x.First()))
        {
            Orders.Add(row); Queue.Add(Map(row));
        }
        var attention = Queue.Count(x => x.RequiresAttention);
        StateText = Queue.Count == 0 ? "No authorized Orders match the current filters." :
            $"Showing {Queue.Count.ToString(CultureInfo.InvariantCulture)} authorized Orders on page {PageNumber.ToString(CultureInfo.InvariantCulture)}.";
        RiskAnnouncement = attention == 0 ? "No displayed Orders require risk attention." :
            $"Warning: {attention.ToString(CultureInfo.InvariantCulture)} displayed Orders require risk attention.";
    }, cancellationToken);

    public Task LoadDetailAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        ClearDetail();
        if (SelectedOrder is null) { StateText = "Select an authorized Order to inspect."; return; }
        var detail = await queries.GetOrderAsync(principal, SelectedOrder.Id, token);
        if (detail is null) { StateText = "Order execution detail is unavailable."; return; }
        if (detail.Order.Id != SelectedOrder.Id) { StateText = "Order execution detail is unavailable."; return; }
        OrderFinancials = $"Filled {Decimal(detail.FilledQuantity)} {detail.Order.QuantityUnit}; gross {Money(detail.GrossAmount, detail.Order.Currency)}; fees {Money(detail.Fees, detail.Order.Currency)}";
        Reservation = detail.ReservationStatus is null ? "No Reservation" :
            $"Reservation {detail.ReservationStatus}; remaining {Money(detail.RemainingReservation ?? 0, detail.Order.Currency)}";
        foreach (var fill in detail.Fills.GroupBy(x => x.Id).Select(x => x.First()))
            Fills.Add(new(fill.Id.ToString(), Decimal(fill.Quantity), Money(fill.Price, fill.Currency),
                Money(fill.Quantity * fill.Price, fill.Currency), Money(fill.Fee, fill.Currency), Utc(fill.ExecutedAt)));
        foreach (var effect in detail.PositionEffects.GroupBy(x => x.Id).Select(x => x.First()))
            Effects.Add(new("Position", effect.Id.ToString(),
                $"{Decimal(effect.Quantity)} {effect.QuantityUnit}; average cost {Money(effect.AverageCost, effect.Currency)}; realized P&L {Money(effect.RealizedProfitLoss, effect.Currency)}",
                "Applied Fills", Utc(effect.UpdatedAt)));
        foreach (var effect in detail.LedgerEffects.GroupBy(x => x.Id).Select(x => x.First()))
            Effects.Add(new("Ledger", effect.Id.ToString(), effect.Amount is not null ? Money(effect.Amount.Value, effect.Currency!) :
                $"{Decimal(effect.Quantity ?? 0)} units", $"{effect.SourceType}:{effect.SourceId}", Utc(effect.EffectiveAt)));
        foreach (var item in detail.Audit.GroupBy(x => (x.Kind, x.Id)).Select(x => x.First()).OrderBy(x => x.At).ThenBy(x => x.Kind).ThenBy(x => x.Id))
            Audit.Add(new(item.Kind, item.Id, item.Status, item.ReasonCode ?? string.Empty,
                item.CorrelationId, Utc(item.At), item.Summary ?? string.Empty));
        OnPropertyChanged(nameof(OrderFinancials)); OnPropertyChanged(nameof(Reservation));
        RiskAnnouncement = detail.Order.Status is OrderStatus.Rejected or OrderStatus.Unknown ||
            detail.Audit.Any(x => IsRisk(x.Status) || IsRisk(x.ReasonCode))
            ? $"Warning: Order {detail.Order.Id} has rejected, unknown, disconnected, stale, or recovery activity."
            : $"Order {detail.Order.Id} has no displayed risk alerts.";
        StateText = $"Loaded authorized chronology for Order {detail.Order.Id}.";
    }, cancellationToken);

    private async Task ChangePageAsync(int delta, CancellationToken token)
    { offset = Math.Max(0, offset + delta); OnPropertyChanged(nameof(PageNumber)); await RefreshAsync(token); }
    public Task NextPageAsync(CancellationToken token = default) => ChangePageAsync(PageSize, token);
    public Task PreviousPageAsync(CancellationToken token = default) => ChangePageAsync(-PageSize, token);

    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token);
        IsBusy = true;
        try { await action(linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { StateText = "Execution request cancelled."; }
        finally { IsBusy = false; }
    }

    private void ClearDetail()
    { Fills.Clear(); Effects.Clear(); Audit.Clear(); OrderFinancials = "No Order selected."; Reservation = string.Empty; OnPropertyChanged(nameof(OrderFinancials)); OnPropertyChanged(nameof(Reservation)); }
    private static ExecutionOrderRow Map(OrderListItem x)
    {
        var risk = x.Status is OrderStatus.Rejected or OrderStatus.Unknown;
        var order = $"{x.Side} {Decimal(x.Quantity)} {x.QuantityUnit} {x.InstrumentId}";
        return new(x.Id, $"{x.Id} / {x.ClientOrderId}", $"Bot {x.TradingBotId}; Portfolio {x.PortfolioId}; Account {x.BrokerAccountId}",
            order, x.Status.ToString().ToUpperInvariant(), Utc(x.CompletedAt ?? x.CreatedAt), risk,
            $"Order {x.Id}; {order}; status {x.Status}; correlation {x.CorrelationId}");
    }
    private static OrderStatus? ParseRiskFilter(string? value) => value?.Trim().ToLowerInvariant() switch
    { "rejected" => OrderStatus.Rejected, "unknown" => OrderStatus.Unknown, "recovery" => OrderStatus.Unknown, _ => null };
    private static bool IsRisk(string? value) => value?.Contains("reject", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Contains("unknown", StringComparison.OrdinalIgnoreCase) == true || value?.Contains("disconnect", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Contains("stale", StringComparison.OrdinalIgnoreCase) == true || value?.Contains("recover", StringComparison.OrdinalIgnoreCase) == true;
    private static string Decimal(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private static string Money(decimal value, string currency) => $"{Decimal(value)} {currency.ToUpperInvariant()}";
    private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

    public ValueTask DisposeAsync() { lifetime.Cancel(); lifetime.Dispose(); return ValueTask.CompletedTask; }
}
