using System.Globalization;

namespace Saas.Billing.Domain.ValueObjects;

public static class MoneyHelper
{
    public static decimal RoundMxn(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static string FormatMxn(decimal amount) =>
        string.Format(CultureInfo.InvariantCulture, "${0:N2} MXN", RoundMxn(amount));
}
