using System.Xml.Linq;
using Domain.Vetis;

namespace VetisApi.Xml;

public static class GetBusinessEntityUserXml
{
    public const string Operation = "GetBusinessEntityUser";

    /// <summary>
    /// Пользователь инициатора должен быть привязан к хозяйствующему субъекту issuerId.
    /// </summary>
    public static XElement Create(VetisCall call)
    {
        var login = call.InitiatorLogin.Trim();
        return new XElement(VetisNs.Mercury + "getBusinessEntityUserRequest",
            new XElement(VetisNs.Mercury + "localTransactionId", Guid.NewGuid().ToString()),
            new XElement(VetisNs.Mercury + "initiator",
                new XElement(VetisNs.VetDocument + "login", login)),
            new XElement(VetisNs.VetDocument + "user",
                new XElement(VetisNs.VetDocument + "login", login)));
    }
}
