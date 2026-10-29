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

    /// <summary>
    /// В документе тип — vetDType. vetDocumentType есть в запросе: пустой тип нельзя оставлять как входящий.
    /// </summary>
    public static IReadOnlyList<VetisIncomingVsd> ReadIncoming(string xml)
    {
        if (!TryParse(xml, out var document))
            return [];

        var result = new List<VetisIncomingVsd>();
        foreach (var element in document.Descendants().Where(IsVetDocumentItem))
        {
            var uuid = Direct(element, "uuid");
            var type = Direct(element, "vetDType");
            if (string.IsNullOrWhiteSpace(uuid) || !string.Equals(type, "INCOMING", StringComparison.OrdinalIgnoreCase))
                continue;

            var raw = element.ToString(SaveOptions.DisableFormatting);
            var consignment = element.Elements().FirstOrDefault(child => child.Name.LocalName == "certifiedConsignment");
            var batch = consignment?.Elements().FirstOrDefault(child => child.Name.LocalName == "batch");
            var source = ReferencedIncomingDocument.Read(raw);
            result.Add(new VetisIncomingVsd
            {
                Uuid = uuid,
                IssueNumber = Direct(element, "issueNumber"),
                IssueDate = Direct(element, "issueDate"),
                Status = Direct(element, "vetDStatus"),
                ProductName = ProductName(batch),
                Volume = batch == null ? "" : Direct(batch, "volume"),
                ConsignorName = PartyName(consignment, "consignor"),
                ConsigneeName = PartyName(consignment, "consignee"),
                SourceNumber = source.Number,
                SourceDate = source.Date,
                SourceType = source.Type,
                ReceivedAt = ReceiptAt(element),
                UpdatedAt = Direct(element, "lastUpdateDate"),
                RawXml = raw
            });
        }

        return result;
    }

    public static int ListedCount(string xml) =>
        TryParse(xml, out var document) ? document.Descendants().Count(IsVetDocumentItem) : 0;

    /// <summary>
    /// Поступление — actualDateTime перехода в UTILIZED. CONFIRMED это оформление, его дату в «Получен» не подставляем.
    /// </summary>
    private static string ReceiptAt(XElement vetDocument)
    {
        DateTimeOffset? earliest = null;
        var text = "";
        foreach (var change in vetDocument.Elements().Where(element => element.Name.LocalName == "statusChange"))
        {
            var actual = Direct(change, "actualDateTime");
            if (!string.Equals(Direct(change, "status"), "UTILIZED", StringComparison.OrdinalIgnoreCase)
                || !VetisDateTime.TryParse(actual, out var instant)
                || (earliest != null && instant >= earliest))
                continue;

            earliest = instant;
            text = actual;
        }

        return text;
    }

    private static bool IsVetDocumentItem(XElement element) =>
        element.Name.LocalName == "vetDocument" && element.Parent?.Name.LocalName == "vetDocumentList";

    private static string ProductName(XElement? batch)
    {
        var item = batch?.Elements().FirstOrDefault(element => element.Name.LocalName == "productItem");
        return item == null ? "" : Direct(item, "name");
    }

    private static string PartyName(XElement? consignment, string party)
    {
        var member = consignment?.Elements().FirstOrDefault(element => element.Name.LocalName == party);
        var named = member?.Descendants().FirstOrDefault(element =>
            element.Name.LocalName is "fullName" or "fio" or "name" && !string.IsNullOrWhiteSpace(element.Value));
        return named?.Value.Trim() ?? "";
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
