using System.Globalization;

namespace Domain.Vetis;

public static class VetisDateTime
{
    /// <summary>
    /// Если пояс не указан, берётся местное время хоста: так Ветис читает issueDate.
    /// </summary>
    public static string Format(DateTimeOffset value) =>
        value.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    public static bool TryParse(string? value, out DateTimeOffset instant)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            instant = default;
            return false;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
            out instant);
    }
}
