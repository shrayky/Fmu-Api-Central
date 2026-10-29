using System.Xml.Linq;

namespace VetisApi.Xml;

public static class GetActivityLocationListXml
{
    public const string Operation = "GetActivityLocationList";

    public const int PageSize = 1000;

    /// <summary>
    /// guid субъекта — IssuerId. Площадки читаются из enterprise, не из businessEntity.
    /// </summary>
    public static XElement Create(string issuerId, int count, int offset)
    {
        return new XElement(VetisNs.Registry + "getActivityLocationListRequest",
            new XElement(VetisNs.Base + "listOptions",
                new XElement(VetisNs.Base + "count", count),
                new XElement(VetisNs.Base + "offset", offset)),
            new XElement(VetisNs.Dictionary + "businessEntity",
                new XElement(VetisNs.Base + "guid", issuerId.Trim())));
    }
}
