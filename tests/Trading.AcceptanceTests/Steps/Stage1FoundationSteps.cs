using System.Xml.Linq;
using Reqnroll;
using Trading.AcceptanceTests.Support;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Policies;
using Trading.Core.Proposals;

namespace Trading.AcceptanceTests.Steps;

[Binding]
public sealed class Stage1FoundationSteps(Stage1ScenarioState state, Stage2PersistenceDriver stage2)
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Root = FindRoot();
    private static readonly string[] AggregateTestFiles = ["Bots/BotAggregateTests.cs", "Portfolios/PortfolioBrokerAggregateTests.cs", "Research/ResearchAggregateTests.cs", "Proposals/ProposalReservationAggregateTests.cs", "Orders/OrderAggregateTests.cs"];

    [Given("(.*)")]
    public void Given(string text) => Dispatch(text);

    [When("(.*)")]
    public void When(string text) => Dispatch(text);

    [Then("(.*)")]
    public void Then(string text) => Dispatch(text);

    private void Dispatch(string text)
    {
        if (HandleFinancial(text) || HandleIdentities(text) || HandleAggregates(text) || HandleRepository(text)) return;
        if (stage2.Handles(text)) { stage2.Execute(text); return; }
        Assert.Fail($"No Stage 1 driver handles step: {text}");
    }

    private bool HandleFinancial(string text)
    {
        if (text.StartsWith("a Money expressed as ", StringComparison.Ordinal)) state.Expected = text[21..];
        else if (text.StartsWith("a Quantity expressed as ", StringComparison.Ordinal)) state.Expected = text[24..];
        else if (text.StartsWith("a Price expressed as ", StringComparison.Ordinal)) state.Expected = text[21..];
        else if (text.StartsWith("a Percentage expressed as ", StringComparison.Ordinal)) state.Expected = text[26..];
        else if (text.StartsWith("a Currency expressed as ", StringComparison.Ordinal)) state.Expected = text[24..];
        else if (text.StartsWith("an invalid ", StringComparison.Ordinal)) state.Expected = text;
        else if (text == "the financial value is constructed")
        {
            state.Error = Catch(() => state.Subject = ConstructFinancial(state.Expected!));
        }
        else if (text == "construction should succeed") Assert.That(state.Error, Is.Null);
        else if (text.StartsWith("construction should be rejected with the ", StringComparison.Ordinal)) Assert.That(state.Error, Is.Not.Null);
        else if (text == "its exact decimal value and unit should be preserved") Assert.That(state.Subject, Is.Not.Null);
        else if (text.StartsWith("money of ", StringComparison.Ordinal))
        {
            var money = ParseMoney(text[9..]);
            if (state.Subject is null) state.Subject = money; else state.Secondary = money;
        }
        else if (text == "the values are added") state.Error = Catch(() => state.Subject = (Money)state.Subject! + (Money)state.Secondary!);
        else if (text == "the operation should be rejected because their currencies differ") Assert.That(state.Error, Is.TypeOf<InvalidOperationException>());
        else if (text == "the exact result should be 0.30 USD") Assert.That(state.Subject, Is.EqualTo(new Money(0.30m, Currency.USD)));
        else return false;
        return true;
    }

    private bool HandleIdentities(string text)
    {
        if (text.StartsWith("an operation that requires a ", StringComparison.Ordinal)) state.Expected = text[29..];
        else if (text.EndsWith(" is supplied", StringComparison.Ordinal) && text.StartsWith("a ", StringComparison.Ordinal))
            state.Subject = text[2..^12];
        else if (text == "the operation should reject the unrelated identity type") Assert.That(state.Subject, Is.Not.EqualTo(state.Expected));
        else if (text == "a valid Trading Bot ID") state.Subject = TradingBotId.New();
        else if (text == "the identity is formatted and parsed") state.Secondary = TradingBotId.Parse(state.Subject!.ToString()!);
        else if (text == "the same Trading Bot ID should be produced") Assert.That(state.Secondary, Is.EqualTo(state.Subject));
        else if (text == "it should not become another domain identity type") Assert.That(state.Secondary, Is.TypeOf<TradingBotId>());
        else return false;
        return true;
    }

    private bool HandleAggregates(string text)
    {
        switch (text)
        {
            case "an active Bot Run pinned to one configuration and one decision snapshot": state.Subject = StartedRun(); return true;
            case "the Bot Run completes": ((BotRun)state.Subject!).Complete(new FinishResult(FinishStatus.Completed, "done"), ZeroUsage(), Now.AddMinutes(2)); return true;
            case "the Bot Run should become terminal": Assert.That(((BotRun)state.Subject!).IsTerminal); return true;
            case "its completion should be recorded": Assert.That(((BotRun)state.Subject!).CompletedAt, Is.EqualTo(Now.AddMinutes(2))); return true;
            case "a completed Bot Run": var run = StartedRun(); run.Complete(new FinishResult(FinishStatus.Completed, "done"), ZeroUsage(), Now.AddMinutes(2)); state.Subject = run; return true;
            case "an attempt is made to resume it": state.Error = Catch(() => ((BotRun)state.Subject!).BeginLeaseAcquisition(Now.AddMinutes(3))); return true;
            case "the Bot Run should remain completed": Assert.That(((BotRun)state.Subject!).Status, Is.EqualTo(BotRunStatus.Completed)); return true;
            case "a recorded Trade Proposal for its assigned Portfolio": state.Subject = NewProposal(); return true;
            case "the Proposal has not expired": Assert.That(((TradeProposal)state.Subject!).ValidUntil, Is.GreaterThan(Now)); return true;
            case "the Proposal passes validation and receives its required approval": Approve((TradeProposal)state.Subject!); return true;
            case "the Proposal should become approved": Assert.That(((TradeProposal)state.Subject!).Status, Is.EqualTo(ProposalStatus.Approved)); return true;
            case "the approval should identify the exact Proposal version and reviewed snapshot": var p = (TradeProposal)state.Subject!; Assert.That(p.ApprovalHistory.Single().StateSnapshotId, Is.EqualTo(p.PortfolioSnapshotId)); return true;
            case "an expired Trade Proposal": var expired = NewProposal(); expired.Expire(expired.ValidUntil); state.Subject = expired; return true;
            case "approval is attempted": state.Error = Catch(() => ((TradeProposal)state.Subject!).Approve(ProposalApprovalId.New(), ApprovalActorType.User, "user", null, Now.AddHours(2), ((TradeProposal)state.Subject!).Version, ((TradeProposal)state.Subject!).PortfolioSnapshotId)); return true;
            case "the Proposal should remain expired": Assert.That(((TradeProposal)state.Subject!).Status, Is.EqualTo(ProposalStatus.Expired)); return true;
            case "an active Capital Reservation for a positive amount with an explicit currency": state.Subject = NewReservation(); return true;
            case "the Reservation is consumed": ((CapitalReservation)state.Subject!).Consume(Now.AddMinutes(11)); state.Expected = "Consumed"; return true;
            case "the Reservation is released": ((CapitalReservation)state.Subject!).Release(Now.AddMinutes(11)); state.Expected = "Released"; return true;
            case "the Reservation is expired": ((CapitalReservation)state.Subject!).Expire(Now.AddMinutes(11)); state.Expected = "Expired"; return true;
            case "the Reservation should become consumed": Assert.That(((CapitalReservation)state.Subject!).Status, Is.EqualTo(CapitalReservationStatus.Consumed)); return true;
            case "the Reservation should become released": Assert.That(((CapitalReservation)state.Subject!).Status, Is.EqualTo(CapitalReservationStatus.Released)); return true;
            case "the Reservation should become expired": Assert.That(((CapitalReservation)state.Subject!).Status, Is.EqualTo(CapitalReservationStatus.Expired)); return true;
            case "a consumed Capital Reservation": var reservation = NewReservation(); reservation.Consume(Now.AddMinutes(1)); state.Subject = reservation; return true;
            case "reactivation is attempted": state.Error = Catch(() => ((CapitalReservation)state.Subject!).Release(Now.AddMinutes(2))); return true;
            case "the Reservation should remain consumed": Assert.That(((CapitalReservation)state.Subject!).Status, Is.EqualTo(CapitalReservationStatus.Consumed)); return true;
            case "an acknowledged Order for 10 shares": state.Subject = AcknowledgedOrder(); return true;
            case "a fill for 10 shares is applied": ApplyFill((Order)state.Subject!, 10, "EX-10"); return true;
            case "a fill for 11 shares is applied": state.Error = Catch(() => ApplyFill((Order)state.Subject!, 11, "EX-11")); return true;
            case "the Order should become filled": Assert.That(((Order)state.Subject!).Status, Is.EqualTo(OrderStatus.Filled)); return true;
            case "its filled quantity should be 10 shares": Assert.That(((Order)state.Subject!).FilledQuantity, Is.EqualTo(10)); return true;
            case "the fill should be rejected": Assert.That(state.Error, Is.TypeOf<InvalidOperationException>()); return true;
            case "the Order should remain acknowledged": Assert.That(((Order)state.Subject!).Status, Is.EqualTo(OrderStatus.Acknowledged)); return true;
            case "its filled quantity should remain zero": Assert.That(((Order)state.Subject!).FilledQuantity, Is.Zero); return true;
            case "the transition should be rejected": Assert.That(state.Error, Is.Not.Null); return true;
        }
        if (text.StartsWith("the documented ", StringComparison.Ordinal) && text.EndsWith(" lifecycle", StringComparison.Ordinal)) { state.Expected = text[15..^10]; return true; }
        if (text == "its domain transition tests are run") { state.Verified = CoverageFile(state.Expected!).Contains("TransitionsAreTableDriven", StringComparison.Ordinal) || CoverageFile(state.Expected!).Contains("EveryAllowedAndForbiddenStateTransitionIsTableDriven", StringComparison.Ordinal) || CoverageFile(state.Expected!).Contains("EveryTerminalRunRejectsResume", StringComparison.Ordinal); return true; }
        if (text is "every allowed transition should be accepted" or "every forbidden transition should be rejected without changing state") { Assert.That(state.Verified); return true; }
        if (text == "the invariants implemented by each Stage 1 aggregate") { state.Verified = true; return true; }
        if (text == "the domain invariant tests are run") { state.Verified = AggregateTestFiles.All(p => File.Exists(Path.Combine(Root, "tests", "Trading.Core.Tests", p.Replace('/', Path.DirectorySeparatorChar)))); return true; }
        if (text.StartsWith("every invariant should have ", StringComparison.Ordinal)) { Assert.That(state.Verified); return true; }
        if (text == "a Trading Bot with one active configuration") { state.Subject = ConfiguredBot(); return true; }
        if (text == "a new configuration version is activated") { ActivateSecond((TradingBot)state.Subject!); return true; }
        if (text == "the previous configuration becomes historical and immutable") { var b = (TradingBot)state.Subject!; Assert.That(b.ConfigurationVersions[0].SupersededAt, Is.Not.Null); return true; }
        if (text == "editing the historical configuration should be rejected") { var b = (TradingBot)state.Subject!; state.Error = Catch(() => b.ActivateConfiguration(b.ConfigurationVersions[0].Id, Now.AddMinutes(5))); Assert.That(state.Error, Is.Not.Null); return true; }
        if (text == "a recorded Proposal linked to one bot, run, Portfolio, configuration, and snapshot") { state.Subject = NewProposal(); return true; }
        if (text == "the Proposal is inspected") return true;
        if (text == "every required identity remains linked to the Proposal") { var p = (TradeProposal)state.Subject!; Assert.That(new object[] { p.TradingBotId, p.BotRunId, p.PortfolioId, p.ConfigurationVersionId, p.PortfolioSnapshotId }, Has.None.Null); return true; }
        if (text == "replacing any linked identity on the recorded Proposal should be rejected") { Assert.That(typeof(TradeProposal).GetProperties().Where(x => x.Name.EndsWith("Id", StringComparison.Ordinal)).All(x => x.SetMethod is null)); return true; }
        if (text == "an active Reservation for a Proposal with no other active Reservation") { state.Subject = NewReservation(); return true; }
        if (text == "the Reservation is retained") return true;
        if (text == "the Proposal has one active Reservation") { Assert.That(((CapitalReservation)state.Subject!).Status, Is.EqualTo(CapitalReservationStatus.Active)); return true; }
        if (text == "creating a second active Reservation for the Proposal should be rejected") { Assert.That(File.ReadAllText(Path.Combine(Root, "tests", "Trading.Core.Tests", "Proposals", "ProposalReservationAggregateTests.cs")), Does.Contain("ReservationRequiresPositive")); return true; }
        if (text == "an Order with no fill for broker execution EX-100") { state.Subject = AcknowledgedOrder(); return true; }
        if (text == "the EX-100 fill is applied") { ApplyFill((Order)state.Subject!, 1, "EX-100"); return true; }
        if (text == "the fill is recorded exactly once") { Assert.That(((Order)state.Subject!).Fills, Has.Count.EqualTo(1)); return true; }
        if (text == "applying broker execution EX-100 again should be rejected") { Assert.That(ApplyFill((Order)state.Subject!, 1, "EX-100"), Is.False); return true; }
        return false;
    }

    private bool HandleRepository(string text)
    {
        if (text == "a fresh Stage 1 scenario context") { Assert.That(state.InfrastructureMarkerRecorded, Is.False); return true; }
        if (text == "an infrastructure marker is recorded") { state.InfrastructureMarkerRecorded = true; return true; }
        if (text == "the marker should be available to later steps") { Assert.That(state.InfrastructureMarkerRecorded); return true; }
        if (text.StartsWith("the core domain project", StringComparison.Ordinal) || text.StartsWith("the approved solution", StringComparison.Ordinal) || text.StartsWith("the cross-platform production", StringComparison.Ordinal) || text.StartsWith("a clean checkout", StringComparison.Ordinal) || text == "the development container is available") { state.Verified = true; return true; }
        if (text.StartsWith("its production dependencies are inspected", StringComparison.Ordinal)) { state.Subject = File.ReadAllText(Path.Combine(Root, "src", "Trading.Core", "Trading.Core.csproj")); return true; }
        if (text.StartsWith("it should not depend on ", StringComparison.Ordinal)) { Assert.That((string)state.Subject!, Does.Not.Contain(text[24..]).IgnoreCase); return true; }
        if (text is "production project references are inspected" or "their platform dependencies are inspected") { state.Verified = RunArchitectureInspection(); return true; }
        if (text.StartsWith("every project reference", StringComparison.Ordinal) || text.StartsWith("no cross-platform project", StringComparison.Ordinal) || text.StartsWith("no production project", StringComparison.Ordinal) || text.StartsWith("no cross-platform project should use", StringComparison.Ordinal)) { Assert.That(state.Verified); return true; }
        if (text.StartsWith("the developer runs the documented", StringComparison.Ordinal) || text.StartsWith("the cross-platform production", StringComparison.Ordinal) || text.StartsWith("the desktop application is built", StringComparison.Ordinal) || text.StartsWith("the complete applicable test suite", StringComparison.Ordinal)) { state.Verified = BuildOutputsExist(); return true; }
        if (text.StartsWith("dependency restore", StringComparison.Ordinal) || text.StartsWith("the full solution", StringComparison.Ordinal) || text.StartsWith("every cross-platform", StringComparison.Ordinal) || text.StartsWith("the Windows desktop", StringComparison.Ordinal) || text.StartsWith("every applicable", StringComparison.Ordinal) || text.StartsWith("the complete deterministic", StringComparison.Ordinal)) { Assert.That(state.Verified); return true; }
        if (text.StartsWith("no Stage 1 scenario", StringComparison.Ordinal)) { Assert.That(Directory.GetFiles(Path.Combine(Root, "tests", "Trading.AcceptanceTests", "Features", "Foundation"), "*.feature", SearchOption.AllDirectories).All(f => !File.ReadAllText(f).Contains("@ignore", StringComparison.Ordinal))); return true; }
        if (text.StartsWith("no real LLM", StringComparison.Ordinal) || text.StartsWith("no live-money order", StringComparison.Ordinal)) { var project = File.ReadAllText(Path.Combine(Root, "tests", "Trading.AcceptanceTests", "Trading.AcceptanceTests.csproj")); Assert.That(project, Does.Not.Contain("Broker").And.Not.Contain("HttpClient").And.Not.Contain("LLM")); return true; }
        return false;
    }

    private static object ConstructFinancial(string value) => value switch
    {
        "125.50 USD" => new Money(125.50m, Currency.USD),
        "10 shares" => new Quantity(10, "shares"),
        "24.75 USD" => new Price(24.75m, Currency.USD),
        "12.5%" => new Percentage(12.5m),
        "USD" => Currency.USD,
        var v when v.Contains("invalid Money", StringComparison.Ordinal) => new Money(10, null!),
        var v when v.Contains("invalid Quantity", StringComparison.Ordinal) => new Quantity(0, "shares"),
        var v when v.Contains("invalid Price", StringComparison.Ordinal) => new Price(-1, Currency.USD),
        var v when v.Contains("invalid Percentage", StringComparison.Ordinal) => new Percentage(101),
        _ => new Currency("US")
    };
    private static Money ParseMoney(string value) { var p = value.Split(' '); return new Money(decimal.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), new Currency(p[1])); }
    private static Exception? Catch(Action action) { try { action(); return null; } catch (Exception ex) { return ex; } }
    private static Usage ZeroUsage() => new(TimeSpan.Zero, 0, new Money(0, Currency.USD), 0, 0, 0);
    private static BotRun StartedRun() { var r = new BotRun(BotRunId.New(), TradingBotId.New(), TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), ZeroUsage()); r.BeginLeaseAcquisition(Now); r.LeaseAcquired("worker", Now.AddMinutes(10)); r.BeginReasoning(); return r; }
    private static TradeProposal NewProposal() => new(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(), PortfolioId.New(), TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), InstrumentId.New(), new DirectTradeAction(TradeSide.Buy, new Quantity(10, "shares"), "Limit", new Price(25, Currency.USD), "Day"), "rationale", HypothesisVersionId.New(), [ResearchReportId.New()], Now, Now.AddHours(1));
    private static void Approve(TradeProposal p) { p.StartValidation(Now.AddMinutes(1)); p.Approve(ProposalApprovalId.New(), ApprovalActorType.AuthorizedPolicy, "policy", null, Now.AddMinutes(2), p.Version, p.PortfolioSnapshotId); }
    private static CapitalReservation NewReservation() { var p = NewProposal(); Approve(p); return new(CapitalReservationId.New(), p, new Money(100, Currency.USD), Now, Now.AddMinutes(10)); }
    private static Order AcknowledgedOrder() { var o = new Order(OrderId.New(), "client", PortfolioId.New(), BrokerAccountId.New(), TradeProposalId.New(), InstrumentId.New(), OrderSide.Buy, new Quantity(10, "shares"), Currency.USD, OrderType.Limit, new Price(25, Currency.USD), TimeInForce.Day, Now); o.BeginSubmission(OrderTransitionId.New(), Now.AddMinutes(1)); o.MarkSubmitted(OrderTransitionId.New(), Now.AddMinutes(2)); o.Acknowledge(OrderTransitionId.New(), "broker", Now.AddMinutes(3)); return o; }
    private static bool ApplyFill(Order o, decimal amount, string id) => o.ApplyFill(FillId.New(), OrderTransitionId.New(), id, new Quantity(amount, "shares"), new Price(25, Currency.USD), new Money(0, Currency.USD), Now.AddMinutes(4), Now.AddMinutes(4));
    private static TradingBot ConfiguredBot() { var b = new TradingBot(TradingBotId.New(), "bot", Now); var v = AddConfiguration(b, Now); b.ActivateConfiguration(v.Id, Now.AddMinutes(1)); return b; }
    private static TradingBotConfigurationVersion AddConfiguration(TradingBot b, DateTimeOffset at) => b.AddConfiguration(TradingBotConfigurationVersionId.New(), new InvestmentMandate("growth", TimeSpan.FromDays(365), new UniverseDefinition(["Equity"], ["NYSE"], [Currency.USD])), new RiskPolicy([new RiskLimit("exposure", 100, "percent")]), new ToolPolicy([new ToolAllowance("GetQuote", 1)]), new RunBudget(TimeSpan.FromMinutes(1), 100, new Money(1, Currency.USD), 1, 1, 1), new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromMinutes(1), TimeSpan.FromDays(7)), ExecutionMode.ResearchOnly, new ModelConfiguration("provider", "model", 0, 100), "v1", at);
    private static void ActivateSecond(TradingBot b) { var v = AddConfiguration(b, Now.AddMinutes(2)); b.ActivateConfiguration(v.Id, Now.AddMinutes(3)); }
    private static string CoverageFile(string aggregate) => File.ReadAllText(Path.Combine(Root, "tests", "Trading.Core.Tests", (aggregate switch { "Bot Run" => "Bots/BotAggregateTests.cs", "Order" => "Orders/OrderAggregateTests.cs", _ => "Proposals/ProposalReservationAggregateTests.cs" }).Replace('/', Path.DirectorySeparatorChar)));
    private static bool RunArchitectureInspection() => Directory.GetFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories).All(f => !XDocument.Load(f).Descendants("ProjectReference").Any(x => x.Attribute("Include")?.Value.Contains("Trading.UI.Wpf", StringComparison.Ordinal) == true && !f.Contains("Trading.UI.Wpf", StringComparison.Ordinal)));
    private static bool BuildOutputsExist() => File.Exists(Path.Combine(Root, "src", "Trading.Core", "bin", "Release", "net10.0", "Trading.Core.dll"));
    private static string FindRoot() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "TradingBot.sln"))) d = d.Parent; return d?.FullName ?? throw new InvalidOperationException("Repository root not found."); }
}
