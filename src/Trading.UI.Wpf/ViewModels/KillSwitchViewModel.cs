using System.Collections.ObjectModel;
using System.Windows.Input;
using Trading.Engine.Operators;
using Trading.UI.Wpf.Commands;

namespace Trading.UI.Wpf.ViewModels;

public sealed class KillSwitchViewModel : ObservableViewModel, IAsyncDisposable
{
    private readonly IOperatorQueries queries;
    private readonly IKillSwitchOperatorService commands;
    private readonly OperatorPrincipal principal;
    private readonly CancellationTokenSource lifetime = new();
    private KillSwitchSummary? selected;
    private KillSwitchDetail? detail;
    private bool isBusy;
    private string reason = string.Empty;
    private string confirmation = string.Empty;
    private string? outcome;

    public KillSwitchViewModel(IOperatorQueries queries, IKillSwitchOperatorService commands, OperatorPrincipal principal)
    {
        this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.principal = principal ?? throw new ArgumentNullException(nameof(principal));
        RefreshCommand = new AsyncCommand<object?>((_, token) => RefreshAsync(token));
        LoadCommand = new AsyncCommand<object?>((_, token) => LoadAsync(token));
        ActivateCommand = new AsyncCommand<object?>((_, token) => ChangeAsync(true, token));
        ClearCommand = new AsyncCommand<object?>((_, token) => ChangeAsync(false, token));
    }

    public ObservableCollection<KillSwitchSummary> Items { get; } = [];
    public ObservableCollection<OperatorKillSwitchHistory> History { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand ActivateCommand { get; }
    public ICommand ClearCommand { get; }
    public bool IsBusy
    {
        get => isBusy;
        private set { if (SetProperty(ref isBusy, value)) OnPropertyChanged(nameof(PendingState)); }
    }
    public string PendingState => IsBusy ? "Operation pending." : "No operation pending.";
    public string Reason { get => reason; set => SetProperty(ref reason, value); }
    public string Confirmation { get => confirmation; set => SetProperty(ref confirmation, value); }
    public string? Outcome { get => outcome; private set => SetProperty(ref outcome, value); }
    public KillSwitchSummary? Selected
    {
        get => selected;
        set { if (SetProperty(ref selected, value)) SetDetail(null); }
    }
    public KillSwitchDetail? Detail { get => detail; private set => SetProperty(ref detail, value); }
    public string EffectiveState => Detail is null ? "Select a kill-switch scope." : Detail.Effective is null
        ? "Effective state: clear."
        : $"Effective state: blocked by {ScopeText(Detail.Effective.Scope)}.";
    public string RequiredActivationConfirmation => Selected is null ? string.Empty : $"ACTIVATE {ScopeText(Selected.Scope)}";
    public string RequiredClearConfirmation => Selected is null ? string.Empty : $"CLEAR {ScopeText(Selected.Scope)}";

    public Task RefreshAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var result = await queries.GetPageAsync<KillSwitchSummary>(principal, OperatorPageKind.RiskAndAudit,
            OperatorResource.Platform, new(Status: "kill-switches"), new(0, 200), token);
        Items.Clear();
        if (result.Status != OperatorResultStatus.Succeeded || result.Value is null)
        { Outcome = "operator.unavailable"; Selected = null; return; }
        foreach (var item in result.Value.Items) Items.Add(item);
        if (Selected is not null) Selected = Items.SingleOrDefault(x => x.Scope == Selected.Scope);
    }, cancellationToken);

    public Task LoadAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        if (Selected is null) { Outcome = "kill_switch.selection_required"; return; }
        await LoadCoreAsync(Selected, token);
    }, cancellationToken);

    public Task ChangeAsync(bool activate, CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        if (Detail is null) { Outcome = "kill_switch.detail_required"; return; }
        if (string.IsNullOrWhiteSpace(Reason)) { Outcome = "kill_switch.reason_required"; return; }
        var required = activate ? RequiredActivationConfirmation : RequiredClearConfirmation;
        if (!string.Equals(Confirmation, required, StringComparison.Ordinal))
        { Outcome = "kill_switch.confirmation_required"; return; }
        var direct = Detail.Direct;
        var result = activate
            ? await commands.ActivateAsync(principal, direct.Scope, direct.Version, Reason.Trim(), Confirmation, token)
            : await commands.ClearAsync(principal, direct.Scope, direct.Version, Reason.Trim(), Confirmation, token);
        Outcome = result.Status switch
        {
            OperatorResultStatus.Succeeded => "kill_switch.succeeded",
            OperatorResultStatus.Conflict => "kill_switch.concurrent_change",
            OperatorResultStatus.Unavailable => "operator.unavailable",
            _ => result.Code,
        };
        if (result.Status != OperatorResultStatus.Succeeded) return;
        Reason = string.Empty;
        Confirmation = string.Empty;
        await RefreshCoreAndSelectAsync(direct.Scope, token);
    }, cancellationToken);

    private async Task RefreshCoreAndSelectAsync(OperatorResource scope, CancellationToken token)
    {
        var result = await queries.GetPageAsync<KillSwitchSummary>(principal, OperatorPageKind.RiskAndAudit,
            OperatorResource.Platform, new(Status: "kill-switches"), new(0, 200), token);
        if (result.Status != OperatorResultStatus.Succeeded || result.Value is null)
        { Items.Clear(); Selected = null; SetDetail(null); Outcome = "operator.unavailable"; return; }
        Items.Clear();
        foreach (var item in result.Value.Items) Items.Add(item);
        Selected = Items.SingleOrDefault(x => x.Scope == scope);
        if (Selected is not null) await LoadCoreAsync(Selected, token);
    }

    private async Task LoadCoreAsync(KillSwitchSummary summary, CancellationToken token)
    {
        var result = await queries.GetPageAsync<KillSwitchDetail>(principal, OperatorPageKind.RiskAndAudit,
            summary.Scope, new(Status: "kill-switch-exact"), new(0, 1), token);
        var loaded = result.Status == OperatorResultStatus.Succeeded ? result.Value?.Items.SingleOrDefault() : null;
        if (loaded is null || loaded.Direct.Scope != summary.Scope || loaded.Direct.Version != summary.Version)
        { SetDetail(null); Outcome = "operator.unavailable"; return; }
        SetDetail(loaded);
    }

    private void SetDetail(KillSwitchDetail? value)
    {
        Detail = value;
        History.Clear();
        if (value is not null) foreach (var entry in value.History) History.Add(entry);
        OnPropertyChanged(nameof(EffectiveState));
        OnPropertyChanged(nameof(RequiredActivationConfirmation));
        OnPropertyChanged(nameof(RequiredClearConfirmation));
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        IsBusy = true;
        try { await action(linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { Outcome = "kill_switch.cancelled"; }
        finally { IsBusy = false; }
    }

    private static string ScopeText(OperatorResource scope) => $"{scope.Kind} {scope.Id}";
    public ValueTask DisposeAsync() { lifetime.Cancel(); lifetime.Dispose(); return ValueTask.CompletedTask; }
}
