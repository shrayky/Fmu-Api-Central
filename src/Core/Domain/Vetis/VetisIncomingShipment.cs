using System.Xml.Linq;

namespace Domain.Vetis;

/// <summary>
/// Список изменений запрашивается по предприятию без роли, поэтому TRANSPORT чужой площадки попадает в тот же журнал.
/// </summary>
public static class VetisIncomingShipment
{
    public static bool IsForeignTransport(string? rawXml, string enterpriseGuid)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
            return false;

        XElement xml;
        try
        {
            xml = XElement.Parse(rawXml);
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }

        var root = xml.Name.LocalName == "vetDocument"
            ? xml
            : xml.Descendants().FirstOrDefault(element => element.Name.LocalName == "vetDocument");
        if (root == null || !string.Equals(Text(root, "vetDType"), "TRANSPORT", StringComparison.OrdinalIgnoreCase))
            return false;

        var consignment = root.Elements().FirstOrDefault(element => element.Name.LocalName == "certifiedConsignment");
        var consignee = consignment?.Elements().FirstOrDefault(element => element.Name.LocalName == "consignee");
        var enterprise = consignee?.Elements().FirstOrDefault(element => element.Name.LocalName == "enterprise");
        var guid = enterprise == null ? "" : Text(enterprise, "guid");

        return !string.Equals(guid, (enterpriseGuid ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string Text(XElement element, string name) =>
        element.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value.Trim() ?? "";
}
