using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Trading.Core.Identifiers;
using Trading.Engine.Operators;
using Trading.UI.Wpf.Commands;

namespace Trading.UI.Wpf.ViewModels;

public sealed class ResearchCatalogViewModel : ObservableViewModel, IAsyncDisposable
{
    public const int PageSize = 25;
    private readonly IOperatorQueries queries;
    private readonly IResearchOperatorService research;
    private readonly OperatorPrincipal principal;
    private readonly CancellationTokenSource lifetime = new();
    private ResearchSummary? selectedReport;
    private ResearchDetail? detail;
    private bool isBusy;
    private string? errorCode;
    private string? search;
    private string? statusFilter;
    private int offset;

    public ResearchCatalogViewModel(IOperatorQueries queries, IResearchOperatorService research,
        OperatorPrincipal principal)
    {
        this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
        this.research = research ?? throw new ArgumentNullException(nameof(research));
        this.principal = principal ?? throw new ArgumentNullException(nameof(principal));
        RefreshCommand = new AsyncCommand<object?>((_, token) => RefreshAsync(token));
        LoadReportCommand = new AsyncCommand<object?>((_, token) => LoadReportAsync(token));
        RequestCommand = new AsyncCommand<object?>((_, token) => RequestAsync(token));
        NextPageCommand = new AsyncCommand<object?>((_, token) => ChangePageAsync(PageSize, token));
        PreviousPageCommand = new AsyncCommand<object?>((_, token) => ChangePageAsync(-PageSize, token));
    }

    public ObservableCollection<ResearchSummary> Items { get; } = [];
    public ObservableCollection<ResearchSummary> Versions { get; } = [];
    public ObservableCollection<OperatorResearchProvenance> Provenance { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand LoadReportCommand { get; }
    public ICommand RequestCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public string? Search { get => search; set => SetProperty(ref search, value); }
    public string? StatusFilter { get => statusFilter; set => SetProperty(ref statusFilter, value); }
    public string RequestingBotId { get; set; } = string.Empty;
    public string RequestSubject { get; set; } = string.Empty;
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? ErrorCode { get => errorCode; private set { if (SetProperty(ref errorCode, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => ErrorCode is not null;
    public int PageNumber => (offset / PageSize) + 1;
    public ResearchSummary? SelectedReport
    {
        get => selectedReport;
        set
        {
            if (!SetProperty(ref selectedReport, value)) return;
            Detail = null;
            Versions.Clear();
            Provenance.Clear();
        }
    }
    public ResearchDetail? Detail
    {
        get => detail;
        private set
        {
            if (!SetProperty(ref detail, value)) return;
            OnPropertyChanged(nameof(ExactIdentity));
            OnPropertyChanged(nameof(Freshness));
            OnPropertyChanged(nameof(Generator));
        }
    }
    public string ExactIdentity => Detail is null ? "No exact report version selected." :
        $"{Detail.Summary.Id} · series {Detail.Summary.SeriesId} · version {Detail.Summary.Version.ToString(CultureInfo.InvariantCulture)}";
    public string Freshness => Detail is null ? string.Empty :
        $"{(Detail.Summary.IsFresh ? "FRESH" : "STALE")} · cutoff {Utc(Detail.Summary.DataCutoff)} · expires {Utc(Detail.Summary.ExpiresAt)}";
    public string Generator => Detail is null ? string.Empty :
        $"{Detail.Generator.Provider}/{Detail.Generator.Model} · prompt {Detail.Generator.PromptVersion} · tools {Detail.Generator.ToolSetVersion} · schema {Detail.Generator.ReportSchemaVersion}";

    public Task RefreshAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var result = await queries.GetPageAsync<ResearchSummary>(principal, OperatorPageKind.Research,
            OperatorResource.Platform, new(Search?.Trim(), StatusFilter?.Trim()), new(offset, PageSize), token);
        if (!Succeeded(result, out var page)) return;
        Items.Clear();
        foreach (var item in page.Items) Items.Add(item);
        var selectedId = SelectedReport?.Id;
        SelectedReport = selectedId is null ? null : Items.SingleOrDefault(x => x.Id == selectedId);
    }, cancellationToken);

    public Task LoadReportAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        if (SelectedReport is null) { ErrorCode = "research_catalog.selection_required"; return; }
        var selected = SelectedReport;
        var exactResource = new OperatorResource(OperatorResourceKind.ResearchReport, selected.Id.ToString());
        var detailResult = await queries.GetPageAsync<ResearchDetail>(principal, OperatorPageKind.Research,
            exactResource, new(Status: $"exact:{selected.SeriesId}:{selected.Version.ToString(CultureInfo.InvariantCulture)}"),
            new(0, 1), token);
        if (!Succeeded(detailResult, out var detailPage) || detailPage.Items.SingleOrDefault() is not { } loaded ||
            loaded.Summary.Id != selected.Id || loaded.Summary.SeriesId != selected.SeriesId || loaded.Summary.Version != selected.Version)
        {
            ErrorCode = "operator.unavailable";
            return;
        }
        Detail = loaded;
        Provenance.Clear();
        foreach (var source in loaded.Provenance) Provenance.Add(source);

        var versionsResult = await queries.GetPageAsync<ResearchSummary>(principal, OperatorPageKind.Research,
            new(OperatorResourceKind.ResearchReport, selected.SeriesId), new(Status: "versions"),
            new(0, OperatorPageRequest.MaximumSize), token);
        if (!Succeeded(versionsResult, out var versionsPage)) { Detail = null; Provenance.Clear(); return; }
        Versions.Clear();
        foreach (var version in versionsPage.Items.Where(x => x.SeriesId == selected.SeriesId).OrderByDescending(x => x.Version))
            Versions.Add(version);
    }, cancellationToken);

    public Task RequestAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        TradingBotId botId;
        try { botId = TradingBotId.Parse(RequestingBotId); }
        catch (ArgumentException) { ErrorCode = "research_catalog.bot_id_invalid"; return; }
        if (string.IsNullOrWhiteSpace(RequestSubject)) { ErrorCode = "research_catalog.subject_required"; return; }
        var result = await research.RequestAsync(principal, botId, RequestSubject.Trim(), token);
        if (result.Status != OperatorResultStatus.Succeeded) { ErrorCode = result.Code; return; }
        await RefreshCoreAsync(token);
    }, cancellationToken);

    private async Task ChangePageAsync(int delta, CancellationToken token)
    {
        offset = Math.Max(0, offset + delta);
        OnPropertyChanged(nameof(PageNumber));
        await RefreshAsync(token);
    }

    private async Task RefreshCoreAsync(CancellationToken token)
    {
        var result = await queries.GetPageAsync<ResearchSummary>(principal, OperatorPageKind.Research,
            OperatorResource.Platform, new(Search?.Trim(), StatusFilter?.Trim()), new(offset, PageSize), token);
        if (!Succeeded(result, out var page)) return;
        Items.Clear();
        foreach (var item in page.Items) Items.Add(item);
    }

    private bool Succeeded<T>(OperatorQueryResult<OperatorPage<T>> result, out OperatorPage<T> page)
    {
        if (result.Status == OperatorResultStatus.Succeeded && result.Value is not null) { page = result.Value; return true; }
        ErrorCode = StableQueryCode(result.Status);
        page = null!;
        return false;
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        IsBusy = true;
        ErrorCode = null;
        try { await action(linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { ErrorCode = "research_catalog.cancelled"; }
        finally { IsBusy = false; }
    }

    private static string StableQueryCode(OperatorResultStatus status) => status switch
    {
        OperatorResultStatus.Unavailable => "operator.unavailable",
        OperatorResultStatus.Conflict => "operator.conflict",
        OperatorResultStatus.Invalid => "operator.invalid",
        OperatorResultStatus.Cancelled => "operator.cancelled",
        _ => "operator.query_failed",
    };
    private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

    public ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        lifetime.Dispose();
        return ValueTask.CompletedTask;
    }
}
