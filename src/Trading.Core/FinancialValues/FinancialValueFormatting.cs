using System.Globalization;

namespace Trading.Core.FinancialValues;

internal static class FinancialValueFormatting
{
    internal static string Format(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
