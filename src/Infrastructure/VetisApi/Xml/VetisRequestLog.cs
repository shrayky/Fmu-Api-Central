using System.Xml.Linq;
using Domain.Configuration;

namespace VetisApi.Xml;

public static class VetisRequestLog
{
    /// <summary>
    /// Запрос сохраняется для просмотра в интерфейсе, поэтому ключ API в нём заменяется маской.
    /// </summary>
    public static string Masked(XElement envelope)
    {
        var copy = new XElement(envelope);
        foreach (var key in copy.Descendants().Where(element => element.Name.LocalName == "apiKey"))
            key.Value = SecretMask.Placeholder;

        return VetisNs.ToUtf8Xml(copy);
    }
}
