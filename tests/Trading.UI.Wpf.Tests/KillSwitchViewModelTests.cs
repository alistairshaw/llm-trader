using System.Collections.Immutable;
using System.Xml.Linq;
using Trading.Engine.Operators;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf.Tests;

[Category("KillSwitchUi")]
public sealed class KillSwitchViewModelTests
{
    private static readonly OperatorResource Scope = new(OperatorResourceKind.Portfolio, "portfolio-7");
    private static readonly DateTimeOffset At = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ExactScopeConfirmationAndFreshVersionAreRequiredThenHierarchyAndHistoryRefresh()
    {
        var gateway = new Gateway();
        await using var model = new KillSwitchViewModel(gateway, gateway,
            new("operator-1", [OperatorAuthority.ReadOperations, OperatorAuthority.ManageKillSwitches]));
        await model.RefreshAsync();
        model.Selected = model.Items.Single();
        await model.LoadAsync();

        model.Reason = "market disruption";
        model.Confirmation = "ACTIVATE Portfolio wrong";
        await model.ChangeAsync(true);
        Assert.That(gateway.Changes, Is.Empty);
        Assert.That(model.Outcome, Is.EqualTo("kill_switch.confirmation_required"));

        model.Confirmation = model.RequiredActivationConfirmation;
        await model.ChangeAsync(true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gateway.Changes.Single(), Is.EqualTo((Scope, 3L, "market disruption", "ACTIVATE Portfolio portfolio-7")));
            Assert.That(model.Detail!.Direct.IsActive, Is.True);
            Assert.That(model.EffectiveState, Does.Contain("blocked by Portfolio portfolio-7"));
            Assert.That(model.History, Has.Count.EqualTo(2));
            Assert.That(model.Outcome, Is.EqualTo("kill_switch.succeeded"));
        }
    }

    [Test]
    public async Task DenialClearsAllPreviouslyVisibleScopeFacts()
    {
        var gateway = new Gateway();
        await using var model = new KillSwitchViewModel(gateway, gateway, new("operator-1", []));
        await model.RefreshAsync();
        Assert.That(model.Items, Is.Not.Empty);

        gateway.Denied = true;
        await model.RefreshAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Items, Is.Empty);
            Assert.That(model.Detail, Is.Null);
            Assert.That(model.Outcome, Is.EqualTo("operator.unavailable"));
        }
    }

    [Test]
    public void ViewExposesExactConfirmationAccessibleNamesStableIdsAndLiveSafetyState()
    {
        var document = XDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "KillSwitchView.xaml"));
        var attributes = document.Descendants().SelectMany(x => x.Attributes()).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(attributes.Count(x => x.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(10));
            Assert.That(attributes.Any(x => x.Name.LocalName.EndsWith(".HeadingLevel", StringComparison.Ordinal)), Is.True);
            Assert.That(attributes.Any(x => x.Name.LocalName.EndsWith(".LiveSetting", StringComparison.Ordinal) && x.Value == "Assertive"), Is.True);
            Assert.That(document.Descendants().Count(x => x.Name.LocalName == "Label" && x.Attribute("Target") is not null), Is.EqualTo(2));
            Assert.That(attributes.Any(x => x.Name.LocalName.EndsWith(".Name", StringComparison.Ordinal) && x.Value.Contains("exact scope", StringComparison.Ordinal)), Is.True);
        }
    }

    private sealed class Gateway : IOperatorQueries, IKillSwitchOperatorService
    {
        private bool active;
        public bool Denied { get; set; }
        public List<(OperatorResource, long, string, string)> Changes { get; } = [];
        private KillSwitchSummary Summary => new(Scope, active, active ? "market disruption" : "clear", "operator-1", At, active ? 4 : 3);
        public Task<OperatorQueryResult<OperatorOverview>> GetOverviewAsync(OperatorPrincipal principal,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OperatorQueryResult<OperatorPage<T>>> GetPageAsync<T>(OperatorPrincipal principal,
            OperatorPageKind page, OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
            CancellationToken cancellationToken)
        {
            if (Denied) return Task.FromResult(new OperatorQueryResult<OperatorPage<T>>(OperatorResultStatus.Unavailable, null));
            object value = typeof(T) == typeof(KillSwitchSummary)
                ? new OperatorPage<KillSwitchSummary>([Summary], 0, null)
                : new OperatorPage<KillSwitchDetail>([new(Summary, active ? Summary : null,
                    active
                        ? [new(true, "market disruption", "operator-1", "ACTIVATE Portfolio portfolio-7", At, 4),
                           new(false, "clear", "operator-1", "CLEAR Portfolio portfolio-7", At.AddMinutes(-1), 3)]
                        : ImmutableArray.Create(new OperatorKillSwitchHistory(false, "clear", "operator-1",
                            "CLEAR Portfolio portfolio-7", At.AddMinutes(-1), 3)))], 0, null);
            return Task.FromResult(new OperatorQueryResult<OperatorPage<T>>(OperatorResultStatus.Succeeded,
                (OperatorPage<T>)value));
        }
        public Task<OperatorCommandResult> ActivateAsync(OperatorPrincipal principal, OperatorResource scope,
            long expectedVersion, string reason, string confirmation, CancellationToken cancellationToken)
        { Changes.Add((scope, expectedVersion, reason, confirmation)); active = true; return Success(); }
        public Task<OperatorCommandResult> ClearAsync(OperatorPrincipal principal, OperatorResource scope,
            long expectedVersion, string reason, string confirmation, CancellationToken cancellationToken)
        { Changes.Add((scope, expectedVersion, reason, confirmation)); active = false; return Success(); }
        private static Task<OperatorCommandResult> Success() => Task.FromResult(new OperatorCommandResult(
            OperatorResultStatus.Succeeded, "operations.kill_switch.changed"));
    }
}
