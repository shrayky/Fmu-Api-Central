using System.Globalization;

namespace Domain.Vetis;

public static class VetisDateTime
{
    /// <summary>
    /// Если пояс не указан, берётся местное время хоста: так Ветис читает issueDate.
    /// </summary>
    public static string Format(DateTimeOffset value) =>
        value.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
}
