using System.Xml.Linq;

namespace Domain.Vetis;

public readonly record struct ReferencedIncomingDocument(string Number, string Date, string Type)
{
    /// <summary>
    /// Входящий документ — транспортный (тип 1–5), связь 1 берётся раньше остальных. Тип 9 накладной не является.
    /// </summary>
    public static ReferencedIncomingDocument Read(string? rawXml)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
            return Empty;

        XElement xml;
        try
        {
            xml = XElement.Parse(rawXml);
        }
        catch (System.Xml.XmlException)
        {
            return Empty;
        }

        var root = xml.Name.LocalName == "vetDocument"
            ? xml
            : xml.Descendants().FirstOrDefault(element => element.Name.LocalName == "vetDocument");
        if (root == null)
            return Empty;

        var transport = root.Elements()
            .Where(element => element.Name.LocalName == "referencedDocument" && IsTransport(element))
            .ToList();
        var chosen = transport.FirstOrDefault(IsPrimaryRelation) ?? transport.FirstOrDefault();
        if (chosen == null)
            return Empty;

        return new ReferencedIncomingDocument(Text(chosen, "issueNumber"), Text(chosen, "issueDate"), Text(chosen, "type"));
    }

    private static ReferencedIncomingDocument Empty => new("", "", "");

    private static bool IsTransport(XElement element) => Text(element, "type") is "1" or "2" or "3" or "4" or "5";

    private static bool IsPrimaryRelation(XElement element) => Text(element, "relationshipType") == "1";

    private static string Text(XElement element, string name) =>
        element.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value.Trim() ?? "";
}
