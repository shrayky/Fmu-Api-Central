using System.Xml.Linq;

namespace Domain.Vetis;

public static class VetisTransport
{
    // Коды transportType из справочника Ветис, не из накладной.
    private static readonly Dictionary<string, string> Types = new(StringComparer.Ordinal)
    {
        ["1"] = "Автомобиль",
        ["2"] = "Железнодорожный",
        ["3"] = "Авиа",
        ["4"] = "Морской контейнер",
        ["5"] = "Морской трюм",
        ["6"] = "Речной",
        ["7"] = "Перегон"
    };

    private static readonly string[] Numbers =
    [
        "vehicleNumber", "trailerNumber", "containerNumber", "wagonNumber", "flightNumber", "shipName"
    ];

    public static string Describe(string? rawXml)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
            return "";

        XElement xml;
        try
        {
            xml = XElement.Parse(rawXml);
        }
        catch (System.Xml.XmlException)
        {
            return "";
        }

        var info = xml.Descendants().FirstOrDefault(element => element.Name.LocalName == "transportInfo");
        var typeCode = Direct(info, "transportType");
        var type = Types.TryGetValue(typeCode, out var typeLabel) ? typeLabel : typeCode;
        var numbers = info == null
            ? ""
            : string.Join(", ", info.Descendants()
                .Where(element => Numbers.Contains(element.Name.LocalName))
                .Select(element => element.Value.Trim())
                .Where(value => value.Length > 0));
        return string.Join(" ", new[] { type, numbers }.Where(part => part.Length > 0));
    }

    private static string Direct(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value.Trim() ?? "";
}
