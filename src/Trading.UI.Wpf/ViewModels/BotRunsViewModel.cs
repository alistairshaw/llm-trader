using System.Collections.ObjectModel;
using System.Windows.Input;
using Trading.Core.Bots;
using Trading.Core.Identifiers;
using Trading.Engine.Operators;
using Trading.UI.Wpf.Commands;

namespace Trading.UI.Wpf.ViewModels;

public sealed class BotRunsViewModel : ObservableViewModel, IAsyncDisposable
{
    private readonly IOperatorQueries queries;
    private readonly IRunOperatorService runs;
    private readonly OperatorPrincipal principal;
    private readonly CancellationTokenSource lifetime = new();
    private RunSummary? selectedRun;
    private RunDetail? detail;
    private bool isBusy;
    private string? errorCode;
    private string? submissionCode;

    public BotRunsViewModel(IOperatorQueries queries, IRunOperatorService runs, OperatorPrincipal principal)
    {
        this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
        this.runs = runs ?? throw new ArgumentNullException(nameof(runs));
        this.principal = principal ?? throw new ArgumentNullException(nameof(principal));
        RefreshCommand = new AsyncCommand<string>((_, token) => RefreshAsync(token));
        TriggerCommand = new AsyncCommand<string>((_, token) => TriggerAsync(token));
        LoadDetailCommand = new AsyncCommand<string>((_, token) => LoadDetailAsync(token));
    }

    public ObservableCollection<RunSummary> ActiveRuns { get; } = [];
    public ObservableCollection<QueuedRunTriggerSummary> QueuedTriggers { get; } = [];
    public ObservableCollection<RunSummary> History { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand TriggerCommand { get; }
    public ICommand LoadDetailCommand { get; }
    public string TradingBotId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public RunSummary? SelectedRun
    {
        get => selectedRun;
        set { if (SetProperty(ref selectedRun, value)) Detail = null; }
    }
    public RunDetail? Detail { get => detail; private set => SetProperty(ref detail, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? ErrorCode { get => errorCode; private set => SetProperty(ref errorCode, value); }
    public string? SubmissionCode { get => submissionCode; private set => SetProperty(ref submissionCode, value); }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => RunPageActionAsync(RefreshCoreAsync, cancellationToken);

    public async Task TriggerAsync(CancellationToken cancellationToken = default)
    {
        TradingBotId botId;
        try
        {
            botId = Trading.Core.Identifiers.TradingBotId.Parse(TradingBotId);
            ArgumentException.ThrowIfNullOrWhiteSpace(Reason);
        }
        catch (ArgumentException)
        {
            ErrorCode = "bot_runs.trigger_invalid";
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        ErrorCode = null;
        IsBusy = true;
        try
        {
            // Submission creates durable work. Page navigation may stop observation, but must not cancel it.
            var result = await runs.TriggerAsync(principal, botId, Reason.Trim(), CancellationToken.None);
            SubmissionCode = result.Code;
            if (result.Status != OperatorResultStatus.Succeeded)
            {
                ErrorCode = result.Code;
                return;
            }
            if (!lifetime.IsCancellationRequested) await RefreshCoreAsync(lifetime.Token);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task LoadDetailAsync(CancellationToken cancellationToken = default) => RunPageActionAsync(async token =>
    {
        if (SelectedRun is null) { ErrorCode = "bot_runs.selection_required"; return; }
        var resource = new OperatorResource(OperatorResourceKind.TradingBot, SelectedRun.TradingBotId.ToString());
        var result = await queries.GetPageAsync<RunDetail>(principal, OperatorPageKind.Runs, resource,
            new(Search: SelectedRun.Id.ToString()), new(0, 1), token);
        if (result.Status != OperatorResultStatus.Succeeded || result.Value?.Items.SingleOrDefault() is not { } value)
        {
            ErrorCode = StableQueryCode(result.Status);
            return;
        }
        Detail = value;
    }, cancellationToken);

    private async Task RefreshCoreAsync(CancellationToken token)
    {
        var resource = OperatorResource.Platform;
        var active = await queries.GetPageAsync<RunSummary>(principal, OperatorPageKind.Runs, resource,
            new(Status: "active"), new(0, OperatorPageRequest.MaximumSize), token);
        var queued = await queries.GetPageAsync<QueuedRunTriggerSummary>(principal, OperatorPageKind.Runs, resource,
            new(Status: "queued"), new(0, OperatorPageRequest.MaximumSize), token);
        var history = await queries.GetPageAsync<RunSummary>(principal, OperatorPageKind.Runs, resource,
            new(Status: "terminal"), new(0, OperatorPageRequest.MaximumSize), token);
        if (active.Status != OperatorResultStatus.Succeeded || active.Value is null ||
            queued.Status != OperatorResultStatus.Succeeded || queued.Value is null ||
            history.Status != OperatorResultStatus.Succeeded || history.Value is null)
        {
            ErrorCode = StableQueryCode(active.Status != OperatorResultStatus.Succeeded ? active.Status :
                queued.Status != OperatorResultStatus.Succeeded ? queued.Status : history.Status);
            return;
        }
        Replace(ActiveRuns, active.Value.Items);
        Replace(QueuedTriggers, queued.Value.Items);
        Replace(History, history.Value.Items);
    }

    private async Task RunPageActionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        IsBusy = true;
        ErrorCode = null;
        try { await action(linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { ErrorCode = "bot_runs.cancelled"; }
        finally { IsBusy = false; }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private static string StableQueryCode(OperatorResultStatus status) => status switch
    {
        OperatorResultStatus.Unavailable => "operator.unavailable",
        OperatorResultStatus.Conflict => "operator.conflict",
        OperatorResultStatus.Invalid => "operator.invalid",
        OperatorResultStatus.Cancelled => "operator.cancelled",
        _ => "operator.query_failed",
    };

    public ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        lifetime.Dispose();
        return ValueTask.CompletedTask;
    }
}
