namespace Trading.Core.FinancialValues;

public sealed record Quantity : IComparable<Quantity>
{
    public Quantity(decimal amount, string unit)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Quantity must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        if (!unit.All(static character => character is >= 'a' and <= 'z'))
        {
            throw new ArgumentException("A quantity unit must contain only lowercase ASCII letters.", nameof(unit));
        }

        Amount = amount;
        Unit = unit;
    }

    public decimal Amount { get; }

    public string Unit { get; }

    public static Quantity operator +(Quantity left, Quantity right)
    {
        left.EnsureCompatible(right);
        return new Quantity(checked(left.Amount + right.Amount), left.Unit);
    }

    public static Quantity operator -(Quantity left, Quantity right)
    {
        left.EnsureCompatible(right);
        return new Quantity(checked(left.Amount - right.Amount), left.Unit);
    }

    public static Quantity operator *(Quantity quantity, decimal multiplier) =>
        new(checked(quantity.Amount * multiplier), quantity.Unit);

    public static Quantity operator /(Quantity quantity, decimal divisor) =>
        new(checked(quantity.Amount / divisor), quantity.Unit);

    public static bool operator <(Quantity left, Quantity right) => left.CompareTo(right) < 0;

    public static bool operator <=(Quantity left, Quantity right) => left.CompareTo(right) <= 0;

    public static bool operator >(Quantity left, Quantity right) => left.CompareTo(right) > 0;

    public static bool operator >=(Quantity left, Quantity right) => left.CompareTo(right) >= 0;

    public int CompareTo(Quantity? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureCompatible(other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() => $"{FinancialValueFormatting.Format(Amount)} {Unit}";

    private void EnsureCompatible(Quantity other)
    {
        if (!string.Equals(Unit, other.Unit, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Quantity units differ: {Unit} and {other.Unit}.");
        }
    }
}
