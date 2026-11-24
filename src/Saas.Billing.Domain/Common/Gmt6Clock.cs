using System.Globalization;

namespace Saas.Billing.Domain.Common;

public static class Gmt6Clock
{
    public static readonly TimeSpan FixedOffset = TimeSpan.FromHours(-6);

    private static readonly string[] StrictIsoFormats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-dd'T'HH:mmzzz",
    ];

    public static string FormatIsoGmt6(DateTimeOffset timestamp)
    {
        return timestamp
            .ToOffset(FixedOffset)
            .ToString("yyyy-MM-ddTHH:mm:ss'-06:00'", CultureInfo.InvariantCulture);
    }

    public static string FormatDateGmt6(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static DateOnly ToDateOnlyGmt6(DateTimeOffset timestamp)
    {
        return DateOnly.FromDateTime(timestamp.ToOffset(FixedOffset).DateTime);
    }

    public static bool TryParseStrictGmt6(string? input, out DateTimeOffset parsedUtc)
    {
        parsedUtc = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (!trimmed.EndsWith("-06:00", StringComparison.Ordinal))
        {
            return false;
        }

        if (!DateTimeOffset.TryParseExact(
                trimmed,
                StrictIsoFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dto))
        {
            return false;
        }

        if (dto.Offset != FixedOffset)
        {
            return false;
        }

        parsedUtc = dto.ToUniversalTime();
        return true;
    }

    public static bool TryParseGmt6Date(string? input, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return DateOnly.TryParseExact(
            input.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    public static (DateTimeOffset StartUtc, DateTimeOffset EndExclusiveUtc) ToUtcDayBounds(
        DateOnly fromDate,
        DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException("fromDate no puede ser mayor que toDate.");
        }

        var startGmt6 = new DateTimeOffset(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, FixedOffset);
        var endExclusiveGmt6 = new DateTimeOffset(toDate.Year, toDate.Month, toDate.Day, 0, 0, 0, FixedOffset).AddDays(1);

        return (startGmt6.ToUniversalTime(), endExclusiveGmt6.ToUniversalTime());
    }
}
