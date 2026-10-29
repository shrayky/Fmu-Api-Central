using System.Xml.Linq;

namespace Domain.Vetis;

public static class VetisXmlFormat
{
    /// <summary>
    /// Ветис отдаёт XML одной строкой: для просмотра нужны отступы. Не XML возвращается без изменений.
    /// </summary>
    public static string Pretty(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return "";

        try
        {
            return XDocument.Parse(xml).ToString();
        }
        catch (System.Xml.XmlException)
        {
            return xml;
        }
    }
}
