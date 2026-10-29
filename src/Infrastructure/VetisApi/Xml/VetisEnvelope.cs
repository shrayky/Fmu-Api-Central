using System.Xml.Linq;
using Domain.Vetis;

namespace VetisApi.Xml;

public static class VetisEnvelope
{
    public const string SubmitAction = "submitApplicationRequest";

    public const string ReceiveAction = "receiveApplicationResult";

    public static XElement Submit(VetisCall call, XElement applicationData)
    {
        return Envelope(new XElement(VetisNs.ApplicationWs + "submitApplicationRequest",
            new XElement(VetisNs.ApplicationWs + "apiKey", call.ApiKey.Trim()),
            new XElement(VetisNs.Application + "application",
                new XElement(VetisNs.Application + "serviceId", call.ServiceId.Trim()),
                new XElement(VetisNs.Application + "issuerId", call.IssuerId.Trim()),
                new XElement(VetisNs.Application + "issueDate", VetisDateTime.Format(DateTimeOffset.Now)),
                new XElement(VetisNs.Application + "data", applicationData))));
    }

    public static XElement Receive(VetisCall call, string applicationId)
    {
        return Envelope(new XElement(VetisNs.ApplicationWs + "receiveApplicationResultRequest",
            new XElement(VetisNs.ApplicationWs + "apiKey", call.ApiKey.Trim()),
            new XElement(VetisNs.ApplicationWs + "issuerId", call.IssuerId.Trim()),
            new XElement(VetisNs.ApplicationWs + "applicationId", applicationId)));
    }

    /// <summary>
    /// Прямой вызов EnterpriseService, без submitApplication.
    /// </summary>
    public static XElement Registry(XElement body)
    {
        return new XElement(VetisNs.Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", VetisNs.Soap),
            new XAttribute(XNamespace.Xmlns + "rdef", VetisNs.Registry),
            new XAttribute(XNamespace.Xmlns + "bs", VetisNs.Base),
            new XAttribute(XNamespace.Xmlns + "dt", VetisNs.Dictionary),
            new XElement(VetisNs.Soap + "Header"),
            new XElement(VetisNs.Soap + "Body", body));
    }

    private static XElement Envelope(XElement body)
    {
        return new XElement(VetisNs.Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", VetisNs.Soap),
            new XAttribute(XNamespace.Xmlns + "apldef", VetisNs.ApplicationWs),
            new XAttribute(XNamespace.Xmlns + "apl", VetisNs.Application),
            new XAttribute(XNamespace.Xmlns + "bs", VetisNs.Base),
            new XAttribute(XNamespace.Xmlns + "dt", VetisNs.Dictionary),
            new XAttribute(XNamespace.Xmlns + "vd", VetisNs.VetDocument),
            new XAttribute(XNamespace.Xmlns + "merc", VetisNs.Mercury),
            new XElement(VetisNs.Soap + "Header"),
            new XElement(VetisNs.Soap + "Body", body));
    }
}
