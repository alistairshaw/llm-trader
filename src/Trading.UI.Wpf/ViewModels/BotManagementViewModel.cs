using System.Collections.ObjectModel;
using System.Windows.Input;
using Trading.Core.Bots;
using Trading.Core.Identifiers;
using Trading.Engine.Operators;
using Trading.UI.Wpf.Commands;

namespace Trading.UI.Wpf.ViewModels;

public sealed class BotManagementViewModel : ObservableViewModel, IAsyncDisposable
{
    private readonly IOperatorQueries queries;
    private readonly IBotOperatorService bots;
    private readonly OperatorPrincipal principal;
    private readonly CancellationTokenSource lifetime = new();
    private BotSummary? selectedBot;
    private BotDetail? detail;
    private bool isBusy;
    private string? errorCode;
    private string? confirmation;

    public BotManagementViewModel(IOperatorQueries queries, IBotOperatorService bots, OperatorPrincipal principal)
    {
        this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
        this.bots = bots ?? throw new ArgumentNullException(nameof(bots));
        this.principal = principal ?? throw new ArgumentNullException(nameof(principal));
        RefreshCommand = new AsyncCommand<string>((_, token) => RefreshAsync(token));
        CreateCommand = new AsyncCommand<string>((_, token) => CreateAsync(token));
        SaveConfigurationCommand = new AsyncCommand<string>((_, token) => SaveConfigurationAsync(token));
        AssignPortfolioCommand = new AsyncCommand<string>((_, token) => AssignPortfolioAsync(token));
        PauseCommand = new AsyncCommand<string>((_, token) => ChangeLifecycleAsync(OperatorCommandKind.PauseBot, token));
        ResumeCommand = new AsyncCommand<string>((_, token) => ChangeLifecycleAsync(OperatorCommandKind.ResumeBot, token));
        RetireCommand = new AsyncCommand<string>((_, token) => ChangeLifecycleAsync(OperatorCommandKind.RetireBot, token));
        ExecutionModes = [ExecutionMode.ResearchOnly, ExecutionMode.HumanApproval, ExecutionMode.PaperTrading];
    }

    public ObservableCollection<BotSummary> Items { get; } = [];
    public IReadOnlyList<ExecutionMode> ExecutionModes { get; }
    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand SaveConfigurationCommand { get; }
    public ICommand AssignPortfolioCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand RetireCommand { get; }
    public BotSummary? SelectedBot
    {
        get => selectedBot;
        set
        {
            if (!SetProperty(ref selectedBot, value)) return;
            Detail = null;
            Confirmation = null;
        }
    }
    public BotDetail? Detail { get => detail; private set => SetProperty(ref detail, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? ErrorCode { get => errorCode; private set { if (SetProperty(ref errorCode, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => ErrorCode is not null;
    public string? Confirmation { get => confirmation; set => SetProperty(ref confirmation, value); }
    public string Name { get; set; } = string.Empty;
    public string Mandate { get; set; } = string.Empty;
    public string RiskPolicyVersion { get; set; } = string.Empty;
    public string ToolPolicyVersion { get; set; } = string.Empty;
    public string SchedulingPolicyVersion { get; set; } = string.Empty;
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.ResearchOnly;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string PortfolioId { get; set; } = string.Empty;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        var result = await queries.GetPageAsync<BotSummary>(principal, OperatorPageKind.Bots,
            OperatorResource.Platform, new(), new(0, OperatorPageRequest.MaximumSize), token);
        if (result.Status != OperatorResultStatus.Succeeded || result.Value is null)
        {
            ErrorCode = StableQueryCode(result.Status);
            return;
        }
        Items.Clear();
        foreach (var item in result.Value.Items) Items.Add(item);
        if (SelectedBot is not null) SelectedBot = Items.SingleOrDefault(x => x.Id == SelectedBot.Id);
    }, cancellationToken);

    public Task LoadDetailAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
    {
        if (SelectedBot is null) { ErrorCode = "bot_management.selection_required"; return; }
        var resource = new OperatorResource(OperatorResourceKind.TradingBot, SelectedBot.Id.ToString());
        var result = await queries.GetPageAsync<BotDetail>(principal, OperatorPageKind.Bots, resource, new(), new(0, 1), token);
        if (result.Status != OperatorResultStatus.Succeeded || result.Value?.Items.SingleOrDefault() is not { } value)
        {
            ErrorCode = StableQueryCode(result.Status);
            return;
        }
        Detail = value;
    }, cancellationToken);

    public Task CreateAsync(CancellationToken cancellationToken = default) => ExecuteAsync(
        token => bots.CreateAsync(principal, Name, token), cancellationToken);

    public Task SaveConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedBot is null) return FailSelection();
        var input = new BotConfigurationInput(Mandate, RiskPolicyVersion, ToolPolicyVersion,
            SchedulingPolicyVersion, ExecutionMode, Model, PromptVersion);
        return ExecuteAsync(token => bots.ConfigureAsync(principal, SelectedBot.Id, SelectedBot.Version, input, token),
            cancellationToken, RequiresModeConfirmation());
    }

    public Task AssignPortfolioAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedBot is null) return FailSelection();
        Trading.Core.Identifiers.PortfolioId portfolioId;
        try { portfolioId = Trading.Core.Identifiers.PortfolioId.Parse(PortfolioId); }
        catch (ArgumentException)
        {
            ErrorCode = "bot_management.portfolio_id_invalid";
            return Task.CompletedTask;
        }
        return ExecuteAsync(token => bots.AssignAsync(principal, SelectedBot.Id, portfolioId, SelectedBot.Version, token), cancellationToken);
    }

    public Task ChangeLifecycleAsync(OperatorCommandKind kind, CancellationToken cancellationToken = default)
    {
        if (SelectedBot is null) return FailSelection();
        if (kind == OperatorCommandKind.RetireBot && Confirmation != "RETIRE")
        {
            ErrorCode = "bot_management.retirement_confirmation_required";
            return Task.CompletedTask;
        }
        return ExecuteAsync(token => kind switch
        {
            OperatorCommandKind.PauseBot => bots.PauseAsync(principal, SelectedBot.Id, SelectedBot.Version, token),
            OperatorCommandKind.ResumeBot => bots.ResumeAsync(principal, SelectedBot.Id, SelectedBot.Version, token),
            OperatorCommandKind.RetireBot => bots.RetireAsync(principal, SelectedBot.Id, SelectedBot.Version, token),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        }, cancellationToken);
    }

    private bool RequiresModeConfirmation() => Detail is not null && Detail.ExecutionMode != ExecutionMode &&
        ExecutionMode != ExecutionMode.ResearchOnly && !string.Equals(Confirmation, ExecutionMode.ToString(), StringComparison.OrdinalIgnoreCase);

    private async Task ExecuteAsync(Func<CancellationToken, Task<OperatorCommandResult>> operation,
        CancellationToken cancellationToken, bool confirmationMissing = false)
    {
        if (confirmationMissing)
        {
            ErrorCode = "bot_management.mode_confirmation_required";
            return;
        }
        await RunAsync(async token =>
        {
            OperatorCommandResult result;
            try { result = await operation(token); }
            catch (ArgumentException) { ErrorCode = "bot_management.validation_failed"; return; }
            if (result.Status != OperatorResultStatus.Succeeded)
            {
                ErrorCode = result.Code;
                return;
            }
            Confirmation = null;
            await RefreshCoreAsync(token);
        }, cancellationToken);
    }

    private async Task RefreshCoreAsync(CancellationToken token)
    {
        var result = await queries.GetPageAsync<BotSummary>(principal, OperatorPageKind.Bots,
            OperatorResource.Platform, new(), new(0, OperatorPageRequest.MaximumSize), token);
        if (result.Status != OperatorResultStatus.Succeeded || result.Value is null)
        {
            ErrorCode = StableQueryCode(result.Status);
            return;
        }
        var selectedId = SelectedBot?.Id;
        Items.Clear();
        foreach (var item in result.Value.Items) Items.Add(item);
        SelectedBot = selectedId is null ? null : Items.SingleOrDefault(x => x.Id == selectedId);
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        IsBusy = true;
        ErrorCode = null;
        try { await action(linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { ErrorCode = "bot_management.cancelled"; }
        finally { IsBusy = false; }
    }

    private Task FailSelection()
    {
        ErrorCode = "bot_management.selection_required";
        return Task.CompletedTask;
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
