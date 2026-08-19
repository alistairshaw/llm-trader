namespace Trading.Core.FinancialValues;

public sealed record Money : IComparable<Money>
{
    public Money(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        left.EnsureCompatible(right);
        return new Money(checked(left.Amount + right.Amount), left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        left.EnsureCompatible(right);
        return new Money(checked(left.Amount - right.Amount), left.Currency);
    }

    public static Money operator -(Money value) => new(checked(-value.Amount), value.Currency);

    public static Money operator *(Money money, decimal multiplier) =>
        new(checked(money.Amount * multiplier), money.Currency);

    public static Money operator /(Money money, decimal divisor) =>
        new(checked(money.Amount / divisor), money.Currency);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public int CompareTo(Money? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureCompatible(other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() => $"{FinancialValueFormatting.Format(Amount)} {Currency}";

    private void EnsureCompatible(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException($"Currencies differ: {Currency} and {other.Currency}.");
        }
    }
}
