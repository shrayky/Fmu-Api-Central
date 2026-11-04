using System.Xml;
using System.Xml.Linq;

namespace VetisApi.Xml;

public static class VetisNs
{
    public static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    public static readonly XNamespace Application = "http://api.vetrf.ru/schema/cdm/application";
    public static readonly XNamespace ApplicationWs = "http://api.vetrf.ru/schema/cdm/application/ws-definitions";
    public static readonly XNamespace Base = "http://api.vetrf.ru/schema/cdm/base";
    public static readonly XNamespace Dictionary = "http://api.vetrf.ru/schema/cdm/dictionary/v2";
    public static readonly XNamespace VetDocument = "http://api.vetrf.ru/schema/cdm/mercury/vet-document/v2";
    public static readonly XNamespace Mercury = "http://api.vetrf.ru/schema/cdm/mercury/g2b/applications/v2";
    public static readonly XNamespace Registry = "http://api.vetrf.ru/schema/cdm/registry/ws-definitions/v2";

    public static string ToUtf8Xml(XElement element)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = false,
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
            element.Save(writer);

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
