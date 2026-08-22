using System.Collections.ObjectModel;
using System.Windows.Input;
using Trading.Core.Proposals;
using Trading.Engine.Operators;
using Trading.UI.Wpf.Commands;

namespace Trading.UI.Wpf.ViewModels;

public sealed class ProposalReviewViewModel : ObservableViewModel, IAsyncDisposable
{
    public const int PageSize = 25;
    private readonly IOperatorQueries queries;
    private readonly IProposalOperatorService decisions;
    private readonly OperatorPrincipal principal;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly CancellationTokenSource lifetime = new();
    private ProposalSummary? selectedProposal;
    private ProposalDetail? detail;
    private bool isBusy;
    private bool confirmDecision;
    private string? errorCode;
    private string? statusFilter = ProposalStatus.AwaitingHumanApproval.ToString();
    private int offset;

    public ProposalReviewViewModel(IOperatorQueries queries, IProposalOperatorService decisions,
        OperatorPrincipal principal, Func<DateTimeOffset>? utcNow = null)
    {
        this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
        this.decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        this.principal = principal ?? throw new ArgumentNullException(nameof(principal));
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        RefreshCommand = new AsyncCommand<object?>((_, token) => RefreshAsync(token));
        LoadProposalCommand = new AsyncCommand<object?>((_, token) => LoadProposalAsync(token));
        ApproveCommand = new AsyncCommand<object?>((_, token) => DecideAsync(true, token));
        RejectCommand = new AsyncCommand<object?>((_, token) => DecideAsync(false, token));
        NextPageCommand = new AsyncCommand<object?>((_, token) => ChangePageAsync(PageSize, token));
        PreviousPageCommand = new AsyncCommand<object?>((_, token) => ChangePageAsync(-PageSize, token));
    }

    public ObservableCollection<ProposalSummary> Items { get; } = [];
    public ObservableCollection<OperatorProposalEvidence> Evidence { get; } = [];
    public ObservableCollection<OperatorGuardrailResult> Guardrails { get; } = [];
    public ObservableCollection<OperatorProposalDecision> DecisionHistory { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand LoadProposalCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand RejectCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public string? StatusFilter { get => statusFilter; set => SetProperty(ref statusFilter, value); }
    public string DecisionReason { get; set; } = string.Empty;
    public bool ConfirmDecision { get => confirmDecision; set => SetProperty(ref confirmDecision, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? ErrorCode { get => errorCode; private set => SetProperty(ref errorCode, value); }
    public int PageNumber => (offset / PageSize) + 1;
    public ProposalSummary? SelectedProposal
    {
        get => selectedProposal;
        set
        {
            if (!SetProperty(ref selectedProposal, value)) return;
            SetDetail(null);
        }
    }
    public ProposalDetail? Detail { get => detail; private set => SetProperty(ref detail, value); }
    public string DecisionEligibility => Detail is null ? "Select and open a Proposal." :
        Detail.Summary.Status != ProposalStatus.AwaitingHumanApproval ? "Terminal Proposal — decisions are disabled." :
        Detail.Summary.ValidUntil <= utcNow() ? "Expired Proposal — approval is disabled." :
        "Awaiting an authorized human decision.";

    public Task RefreshAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var selectedId = SelectedProposal?.Id;
        await RefreshCoreAsync(token);
        SelectedProposal = selectedId is null ? null : Items.SingleOrDefault(x => x.Id == selectedId);
    }, cancellationToken);

    public Task LoadProposalAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        if (SelectedProposal is null) { ErrorCode = "proposal_review.selection_required"; return; }
        await LoadCoreAsync(SelectedProposal, token);
    }, cancellationToken);

    public Task DecideAsync(bool approve, CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        if (Detail is null) { ErrorCode = "proposal_review.detail_required"; return; }
        if (!ConfirmDecision) { ErrorCode = "proposal_review.confirmation_required"; return; }
        if (Detail.Summary.Status != ProposalStatus.AwaitingHumanApproval || Detail.Summary.ValidUntil <= utcNow())
        { ErrorCode = "proposal_review.ineligible"; return; }
        if (!approve && string.IsNullOrWhiteSpace(DecisionReason))
        { ErrorCode = "proposal_review.rejection_reason_required"; return; }

        var reviewed = Detail;
        var result = approve
            ? await decisions.ApproveAsync(principal, reviewed.Summary.Id, reviewed.Summary.Version,
                NullIfWhiteSpace(DecisionReason), token)
            : await decisions.RejectAsync(principal, reviewed.Summary.Id, reviewed.Summary.Version,
                DecisionReason.Trim(), token);
        if (result.Status != OperatorResultStatus.Succeeded) { ErrorCode = result.Code; return; }

        ConfirmDecision = false;
        DecisionReason = string.Empty;
        await RefreshCoreAsync(token);
        var refreshed = Items.SingleOrDefault(x => x.Id == reviewed.Summary.Id);
        SelectedProposal = refreshed;
        if (refreshed is not null) await LoadCoreAsync(refreshed, token);
    }, cancellationToken);

    private async Task RefreshCoreAsync(CancellationToken token)
    {
        var result = await queries.GetPageAsync<ProposalSummary>(principal, OperatorPageKind.Proposals,
            OperatorResource.Platform, new(Status: StatusFilter?.Trim()), new(offset, PageSize), token);
        if (!Succeeded(result, out var page)) return;
        Items.Clear();
        foreach (var item in page.Items) Items.Add(item);
    }

    private async Task LoadCoreAsync(ProposalSummary selected, CancellationToken token)
    {
        var result = await queries.GetPageAsync<ProposalDetail>(principal, OperatorPageKind.Proposals,
            new(OperatorResourceKind.TradeProposal, selected.Id.ToString()), new(Status: "exact"), new(0, 1), token);
        if (!Succeeded(result, out var page) || page.Items.SingleOrDefault() is not { } loaded ||
            loaded.Summary.Id != selected.Id || loaded.Summary.Version != selected.Version)
        { ErrorCode = "operator.unavailable"; SetDetail(null); return; }
        SetDetail(loaded);
    }

    private void SetDetail(ProposalDetail? value)
    {
        Detail = value;
        Evidence.Clear(); Guardrails.Clear(); DecisionHistory.Clear();
        if (value is not null)
        {
            foreach (var item in value.Evidence) Evidence.Add(item);
            foreach (var item in value.Guardrails) Guardrails.Add(item);
            foreach (var item in value.Decisions) DecisionHistory.Add(item);
        }
        OnPropertyChanged(nameof(DecisionEligibility));
    }

    private async Task ChangePageAsync(int delta, CancellationToken token)
    {
        offset = Math.Max(0, offset + delta);
        OnPropertyChanged(nameof(PageNumber));
        await RefreshAsync(token);
    }

    private bool Succeeded<T>(OperatorQueryResult<OperatorPage<T>> result, out OperatorPage<T> page)
    {
        if (result.Status == OperatorResultStatus.Succeeded && result.Value is not null)
        {
            page = result.Value;
            return true;
        }
        ErrorCode = result.Status == OperatorResultStatus.Unavailable ? "operator.unavailable" : result.Status switch
        {
            OperatorResultStatus.Conflict => "operator.conflict",
            OperatorResultStatus.Invalid => "operator.invalid",
            OperatorResultStatus.Cancelled => "operator.cancelled",
            _ => "operator.query_failed",
        };
        page = null!;
        return false;
    }
    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        IsBusy = true; ErrorCode = null;
        try { await action(linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { ErrorCode = "proposal_review.cancelled"; }
        finally { IsBusy = false; }
    }
    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        lifetime.Dispose();
        return ValueTask.CompletedTask;
    }
}
