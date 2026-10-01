using System.Globalization;
using System.Text.RegularExpressions;

namespace Domain.TrueApiIntegration;

public static partial class CryptoProLicenseView
{
    [GeneratedRegex(@"\b(\d{2})\.(\d{2})\.(\d{4})\b")]
    private static partial Regex DottedDate();

    [GeneratedRegex(@"\b(\d{4})-(\d{2})-(\d{2})\b")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"\b(\d{2})/(\d{2})/(\d{4})\b")]
    private static partial Regex SlashDate();

    [GeneratedRegex(@"(\d+)\s*day", RegexOptions.IgnoreCase)]
    private static partial Regex DayCount();

    public static CryptoProLicenseInfo Parse(string? viewText, DateOnly? today = null)
    {
        if (string.IsNullOrWhiteSpace(viewText))
            return CryptoProLicenseInfo.Unknown;

        if (IsPermanent(viewText))
            return CryptoProLicenseInfo.WithoutExpiry;

        foreach (var line in viewText.Split('\n'))
        {
            if (!IsExpiryLine(line))
                continue;

            var date = ReadDate(line) ?? ReadDayCount(line, today ?? DateOnly.FromDateTime(DateTime.Today));
            if (date != null)
                return new CryptoProLicenseInfo(false, date);
        }

        return CryptoProLicenseInfo.Unknown;
    }

    /// <summary>
    /// cpconfig пишет Expires/Срок действия отдельной строкой, серийник датой не является.
    /// </summary>
    private static bool IsExpiryLine(string line)
    {
        return line.Contains("expir", StringComparison.OrdinalIgnoreCase)
            || line.Contains("valid till", StringComparison.OrdinalIgnoreCase)
            || line.Contains("истек", StringComparison.OrdinalIgnoreCase)
            || line.Contains("срок действ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPermanent(string text)
    {
        return text.Contains("permanent", StringComparison.OrdinalIgnoreCase)
            || text.Contains("unlimited", StringComparison.OrdinalIgnoreCase)
            || text.Contains("бессроч", StringComparison.OrdinalIgnoreCase)
            || text.Contains("неогранич", StringComparison.OrdinalIgnoreCase);
    }

    private static DateOnly? ReadDate(string line)
    {
        var dotted = DottedDate().Match(line);
        if (dotted.Success
            && DateOnly.TryParseExact(
                $"{dotted.Groups[1].Value}.{dotted.Groups[2].Value}.{dotted.Groups[3].Value}",
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dottedDate))
            return dottedDate;

        var slash = SlashDate().Match(line);
        if (slash.Success
            && DateOnly.TryParseExact(
                $"{slash.Groups[1].Value}/{slash.Groups[2].Value}/{slash.Groups[3].Value}",
                "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var slashDate))
            return slashDate;

        var iso = IsoDate().Match(line);
        if (iso.Success
            && DateOnly.TryParseExact(
                $"{iso.Groups[1].Value}-{iso.Groups[2].Value}-{iso.Groups[3].Value}",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var isoDate))
            return isoDate;

        return null;
    }

    /// <summary>
    /// Linux cpconfig пишет «Expires: 78 day(s)», без календарной даты.
    /// </summary>
    private static DateOnly? ReadDayCount(string line, DateOnly today)
    {
        var match = DayCount().Match(line);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var days))
            return null;

        return today.AddDays(days);
    }
}

public sealed record CryptoProLicenseInfo(bool Permanent, DateOnly? ExpiresAt)
{
    public static CryptoProLicenseInfo Unknown { get; } = new(false, null);

    public static CryptoProLicenseInfo WithoutExpiry { get; } = new(true, null);
}
