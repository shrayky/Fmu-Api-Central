using System.Xml.Linq;
using Domain.Vetis;

namespace VetisApi.Xml;

public static class GetVetDocumentChangesListXml
{
    public const string Operation = "GetVetDocumentChangesList";

    /// <summary>
    /// count — размер страницы, не предел выборки. Ноль и пустое значение Ветис считает как 100.
    /// </summary>
    public static XElement Create(VetisCall call, VetisIncomingRequest request)
    {
        var count = request.Count > 0 ? request.Count : IncomingChangesWindow.PageSize;
        var offset = Math.Max(0, request.Offset);
        return new XElement(VetisNs.Mercury + "getVetDocumentChangesListRequest",
            new XElement(VetisNs.Mercury + "localTransactionId", Guid.NewGuid().ToString()),
            new XElement(VetisNs.Mercury + "initiator",
                new XElement(VetisNs.VetDocument + "login", call.InitiatorLogin.Trim())),
            new XElement(VetisNs.Base + "listOptions",
                new XElement(VetisNs.Base + "count", count),
                new XElement(VetisNs.Base + "offset", offset)),
            new XElement(VetisNs.Base + "updateDateInterval",
                new XElement(VetisNs.Base + "beginDate", VetisDateTime.Format(request.Begin)),
                new XElement(VetisNs.Base + "endDate", VetisDateTime.Format(request.End))),
            new XElement(VetisNs.Dictionary + "enterpriseGuid", request.EnterpriseGuid.Trim()));
    }

    public static string Describe(VetisIncomingRequest request)
    {
        var count = request.Count > 0 ? request.Count : IncomingChangesWindow.PageSize;
        return $"Период {request.Begin:dd.MM.yyyy HH:mm} – {request.End:dd.MM.yyyy HH:mm}; смещение {Math.Max(0, request.Offset)}, количество {count}";
    }
}
