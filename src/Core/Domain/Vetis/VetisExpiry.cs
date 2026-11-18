using System.Globalization;
using System.Xml.Linq;

namespace Domain.Vetis;

/// <summary>
/// Срок годности партии из сохранённого XML ВСД. Дата выработки в том же блоке firstDate не подходит.
/// </summary>
public static class VetisExpiry
{
    public static string Text(string? rawXml)
    {
        if (!TryParse(rawXml, out var xml))
            return "";

        var dates = Dates(xml).ToList();
        return dates.Count > 0 ? Format(dates.Min()) : Informal(xml);
    }

    /// <summary>
    /// Текст срока в минимум не входит: его нельзя сравнить с датой.
    /// </summary>
    public static string Earliest(IEnumerable<string?> rawXmls)
    {
        DateOnly? earliest = null;
        foreach (var rawXml in rawXmls ?? [])
        {
            if (!TryParse(rawXml, out var xml))
                continue;

            foreach (var date in Dates(xml))
            {
                if (earliest == null || date < earliest)
                    earliest = date;
            }
        }

        return earliest == null ? "" : Format(earliest.Value);
    }

    /// <summary>
    /// Сегодня ещё не истёк: красным становится только предыдущий календарный день сервера.
    /// Текст без даты сравнить нельзя.
    /// </summary>
    public static bool Expired(string? text, DateOnly today) =>
        DateOnly.TryParseExact((text ?? "").Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        && date < today;

    private static IEnumerable<DateOnly> Dates(XElement xml)
    {
        // Фрагмент срока приходит корнем: Descendants его не видит.
        foreach (var expiry in xml.DescendantsAndSelf().Where(element => element.Name.LocalName == "expiryDate"))
        {
            foreach (var part in expiry.Elements().Where(element => element.Name.LocalName is "firstDate" or "secondDate"))
            {
                if (TryDate(part, out var date))
                    yield return date;
            }
        }
    }

    /// <summary>
    /// Неполный срок Меркурия сравнивается с первого числа: так более ранняя неполная дата остаётся меньшей.
    /// </summary>
    private static bool TryDate(XElement part, out DateOnly date)
    {
        date = default;
        if (!int.TryParse(Direct(part, "year"), NumberStyles.None, CultureInfo.InvariantCulture, out var year) || year < 1)
            return false;

        var monthText = Direct(part, "month");
        var month = 1;
        if (monthText.Length > 0 && (!int.TryParse(monthText, NumberStyles.None, CultureInfo.InvariantCulture, out month) || month is < 1 or > 12))
            return false;

        var dayText = Direct(part, "day");
        var day = 1;
        if (dayText.Length > 0)
        {
            if (!int.TryParse(dayText, NumberStyles.None, CultureInfo.InvariantCulture, out day))
                return false;

            if (day < 1 || day > DateTime.DaysInMonth(year, month))
                return false;
        }

        date = new DateOnly(year, month, day);
        return true;
    }

    private static string Informal(XElement xml) =>
        xml.DescendantsAndSelf()
            .Where(element => element.Name.LocalName == "informalDate" && element.Parent?.Name.LocalName == "expiryDate")
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => value.Length > 0) ?? "";

    private static string Format(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static string Direct(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value.Trim() ?? "";

    private static bool TryParse(string? rawXml, out XElement xml)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
        {
            xml = new XElement("empty");
            return false;
        }

        try
        {
            xml = XElement.Parse(rawXml);
            return true;
        }
        catch (System.Xml.XmlException)
        {
            xml = new XElement("empty");
            return false;
        }
    }
}
