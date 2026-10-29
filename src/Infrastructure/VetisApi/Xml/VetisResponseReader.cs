using System.Globalization;
using System.Xml.Linq;
using Domain.Vetis;

namespace VetisApi.Xml;

public static class VetisResponseReader
{
    public static string? ApplicationId(string xml) => First(xml, "applicationId");

    public static string? ApplicationStatus(string xml) => First(xml, "status");

    public static string? ErrorText(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        if (!TryParse(xml, out var document))
            return Trim(xml);

        var parts = document.Descendants()
            .Where(element => element.Name.LocalName is "faultstring" or "error")
            .Select(element => element.Value.Trim())
            .Where(value => value.Length > 0)
            .Distinct()
            .ToList();

        return parts.Count == 0 ? null : Trim(string.Join("; ", parts));
    }

    public static int? ListTotal(string xml, string localName)
    {
        if (!TryParse(xml, out var document))
            return null;

        var list = document.Descendants().FirstOrDefault(element => element.Name.LocalName == localName);
        var total = list?.Attribute("total")?.Value;
        return int.TryParse(total, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>
    /// Площадки из activityLocationList: guid и название берутся у enterprise.
    /// </summary>
    public static IReadOnlyList<VetisEnterprise> ReadEnterprises(string xml)
    {
        if (!TryParse(xml, out var document))
            return [];

        var list = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "activityLocationList");
        if (list == null)
            return [];

        var result = new List<VetisEnterprise>();
        foreach (var location in list.Elements().Where(element => element.Name.LocalName is "location" or "activityLocation"))
        {
            var enterprise = location.Elements().FirstOrDefault(element => element.Name.LocalName == "enterprise");
            if (enterprise == null)
                continue;

            var guid = Direct(enterprise, "guid");
            if (string.IsNullOrWhiteSpace(guid))
                continue;

            result.Add(new VetisEnterprise
            {
                Guid = guid,
                Name = Direct(enterprise, "name"),
                Address = enterprise.Descendants()
                    .FirstOrDefault(element => element.Name.LocalName == "addressView")
                    ?.Value
                    .Trim() ?? ""
            });
        }

        return result;
    }

    private static string? First(string xml, string localName)
    {
        if (!TryParse(xml, out var document))
            return null;

        return document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value
            .Trim();
    }

    private static bool TryParse(string xml, out XElement document)
    {
        try
        {
            document = XElement.Parse(xml);
            return true;
        }
        catch (System.Xml.XmlException)
        {
            document = new XElement("empty");
            return false;
        }
    }

    private static string Direct(XElement parent, string localName)
    {
        return parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value.Trim() ?? "";
    }

    private static string Trim(string value)
    {
        var compact = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return compact.Length <= 2000 ? compact : compact[..2000];
    }
}
