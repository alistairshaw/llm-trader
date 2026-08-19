using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Core.Portfolios;

public enum PortfolioStatus { Active, Paused, Closed }

public sealed class Portfolio
{
    private bool _hasFinancialActivity;

    public Portfolio(PortfolioId id, string name, Currency baseCurrency, Money capitalAllocation,
        decimal cashReservePercentage, DateTimeOffset createdAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = PortfolioValidation.Required(name, nameof(name));
        BaseCurrency = baseCurrency ?? throw new ArgumentNullException(nameof(baseCurrency));
        if (capitalAllocation.Currency != baseCurrency || capitalAllocation.Amount < 0) throw new ArgumentException("Capital allocation must be non-negative and use the base currency.", nameof(capitalAllocation));
        if (cashReservePercentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(cashReservePercentage));
        CapitalAllocation = capitalAllocation;
        CashReservePercentage = cashReservePercentage;
        CreatedAt = PortfolioValidation.Utc(createdAt, nameof(createdAt));
        Status = PortfolioStatus.Active;
    }

    public PortfolioId Id { get; }
    public string Name { get; }
    public Currency BaseCurrency { get; private set; }
    public BrokerAccountId? BrokerAccountId { get; private set; }
    public TradingBotId? AssignedTradingBotId { get; private set; }
    public PortfolioStatus Status { get; private set; }
    public Money CapitalAllocation { get; private set; }
    public decimal CashReservePercentage { get; }
    public DateTimeOffset CreatedAt { get; }
    public bool HasFinancialActivity => _hasFinancialActivity;
    public bool CanAcceptNewActivity => Status != PortfolioStatus.Closed;

    public void AssignTradingBot(TradingBotId botId)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(botId);
        if (AssignedTradingBotId is not null && AssignedTradingBotId != botId) throw new InvalidOperationException("A portfolio can have at most one assigned Trading Bot.");
        AssignedTradingBotId = botId;
    }

    public void AssociateBrokerAccount(BrokerAccountId accountId)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(accountId);
        if (BrokerAccountId is not null && BrokerAccountId != accountId) throw new InvalidOperationException("A portfolio is already associated with a broker account.");
        BrokerAccountId = accountId;
    }

    public void ChangeBaseCurrency(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        if (_hasFinancialActivity) throw new InvalidOperationException("Base currency cannot change after financial activity begins.");
        if (CapitalAllocation.Amount != 0) throw new InvalidOperationException("Base currency cannot change while capital is allocated.");
        BaseCurrency = currency;
        CapitalAllocation = Money.Zero(currency);
    }

    public void RecordCapitalChange(Money change, string auditReference)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(change);
        PortfolioValidation.Required(auditReference, nameof(auditReference));
        if (change.Currency != BaseCurrency) throw new ArgumentException("Capital change must use the base currency.", nameof(change));
        var next = CapitalAllocation + change;
        if (next.Amount < 0) throw new InvalidOperationException("Capital allocation cannot be negative.");
        CapitalAllocation = next;
        _hasFinancialActivity = true;
    }

    public void Pause() { EnsureOpen(); Status = PortfolioStatus.Paused; }
    public void Activate() { EnsureOpen(); Status = PortfolioStatus.Active; }
    public void Close() => Status = PortfolioStatus.Closed;
    public void AuthorizeDecisionSnapshot() { EnsureOpen(); if (AssignedTradingBotId is null) throw new InvalidOperationException("A Trading Bot must be assigned."); }
    private void EnsureOpen() { if (Status == PortfolioStatus.Closed) throw new InvalidOperationException("A closed portfolio rejects new activity."); }
}

internal static class PortfolioValidation
{
    public static string Required(string? value, string name) { ArgumentNullException.ThrowIfNull(value, name); var result = value.Trim(); if (result.Length == 0) throw new ArgumentException("Value is required.", name); return result; }
    public static DateTimeOffset Utc(DateTimeOffset value, string name) { if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be expressed in UTC.", name); return value; }
}
