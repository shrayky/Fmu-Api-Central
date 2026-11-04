using System.Globalization;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using Domain.Vetis;

namespace VetisApi.Xml;

public static class ProcessIncomingConsignmentXml
{
    public const string Operation = "ProcessIncomingConsignment";

    // В принятой заявке нет собственника, классификаций и номенклатуры внутри origin.
    private static readonly string[] ConsignmentFields =
    [
        "productType", "product", "subProduct", "productItem", "volume", "unit",
        "dateOfProduction", "expiryDate", "batchID", "perishable", "origin",
        "lowGradeCargo", "packageList"
    ];

    private static readonly string[] RequiredBatchFields =
    [
        "productType", "product", "subProduct", "productItem", "volume", "unit",
        "dateOfProduction", "perishable", "origin", "lowGradeCargo"
    ];

    public static Result<XElement> Create(VetisCall call, string rawXml, VetisQuenchOrder order, DateOnly discrepancyDate)
    {
        if (!TryParse(rawXml, out var vetDocument))
            return Result.Failure<XElement>("В ВСД нет исходного XML, гашение не отправлено.");

        var uuid = Direct(vetDocument, "uuid");
        if (string.IsNullOrWhiteSpace(uuid))
            return Result.Failure<XElement>("В ВСД нет uuid, гашение не отправлено.");

        var consignment = DirectElement(vetDocument, "certifiedConsignment");
        if (consignment == null)
            return Result.Failure<XElement>("В ВСД нет certifiedConsignment, это не транспортная партия.");

        var batch = DirectElement(consignment, "batch");
        if (batch == null)
            return Result.Failure<XElement>("В ВСД нет certifiedConsignment/batch, гашение не отправлено.");

        foreach (var field in RequiredBatchFields)
        {
            if (DirectElement(batch, field) == null)
                return Result.Failure<XElement>($"В партии ВСД нет поля {field}, гашение не отправлено.");
        }

        var consignor = Member(consignment, "consignor");
        if (consignor.IsFailure)
            return Result.Failure<XElement>(consignor.Error);

        var consignee = Member(consignment, "consignee");
        if (consignee.IsFailure)
            return Result.Failure<XElement>(consignee.Error);

        if (DirectElement(consignment, "transportInfo") == null)
            return Result.Failure<XElement>("В ВСД нет transportInfo: без номера транспортного средства Ветис не принимает гашение.");

        var waybill = Waybill(vetDocument);
        if (waybill.IsFailure)
            return Result.Failure<XElement>(waybill.Error);

        if (!order.KeepIncomingVolume)
        {
            var volume = DirectElement(batch, "volume");
            if (volume != null)
                volume.Value = order.ActualVolume;
        }

        var login = call.InitiatorLogin.Trim();
        var delivery = new XElement(VetisNs.Mercury + "delivery",
            new XElement(VetisNs.VetDocument + "deliveryDate", VetisDateTime.Format(DateTimeOffset.Now)),
            consignor.Value,
            consignee.Value,
            AsConsignment(batch));

        var broker = DirectElement(consignment, "broker");
        if (broker != null)
        {
            var copy = new XElement(VetisNs.VetDocument + "broker");
            KeepGuid(broker, copy);
            delivery.Add(copy);
        }

        delivery.Add(new XElement(DirectElement(consignment, "transportInfo")!));
        var storage = DirectElement(consignment, "transportStorageType");
        if (storage != null)
            delivery.Add(new XElement(storage));

        var forms = new XElement(VetisNs.VetDocument + "accompanyingForms");
        if (waybill.Value != null)
            forms.Add(waybill.Value);

        forms.Add(new XElement(VetisNs.VetDocument + "vetCertificate",
            new XElement(VetisNs.Base + "uuid", uuid)));
        delivery.Add(forms);

        var facts = new XElement(VetisNs.Mercury + "deliveryFacts",
            new XElement(VetisNs.VetDocument + "vetCertificatePresence", "ELECTRONIC"),
            Inspection("docInspection", login),
            Inspection("vetInspection", login),
            new XElement(VetisNs.VetDocument + "decision", order.Decision));
        // Без акта на расхождение Меркурий не оформляет возвратный ВСД.
        if (order.Decision != VetisQuench.AcceptAll)
            facts.Add(Discrepancy(order, discrepancyDate));

        return Result.Success(new XElement(VetisNs.Mercury + "processIncomingConsignmentRequest",
            new XElement(VetisNs.Mercury + "localTransactionId", Guid.NewGuid().ToString()),
            new XElement(VetisNs.Mercury + "initiator",
                new XElement(VetisNs.VetDocument + "login", login)),
            delivery,
            facts));
    }

    public static string Describe(string uuid, VetisQuenchOrder order) =>
        $"Гашение {uuid}: {order.Decision}, факт {order.ActualVolume}";

    private static XElement Discrepancy(VetisQuenchOrder order, DateOnly date) =>
        new(VetisNs.VetDocument + "discrepancyReport",
            new XElement(VetisNs.VetDocument + "issueDate", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(VetisNs.VetDocument + "reason",
                new XElement(VetisNs.VetDocument + "name", "Расхождение в количестве")),
            new XElement(VetisNs.VetDocument + "description", $"Принято {order.ActualVolume} из {order.IncomingVolume}"));

    private static XElement AsConsignment(XElement batch)
    {
        var consignment = new XElement(VetisNs.VetDocument + "consignment");
        foreach (var name in ConsignmentFields)
        {
            foreach (var field in batch.Elements().Where(element => element.Name.LocalName == name))
            {
                var copy = new XElement(field);
                if (name == "origin")
                    copy.Elements().Where(element => element.Name.LocalName == "productItem").Remove();

                if (name is "dateOfProduction" or "expiryDate")
                    RewriteDateParts(copy);
                else if (name != "packageList")
                    KeepGuid(copy);

                consignment.Add(copy);
            }
        }

        return consignment;
    }

    private static Result<XElement> Member(XElement consignment, string name)
    {
        var source = DirectElement(consignment, name);
        if (source == null)
            return Result.Failure<XElement>($"В ВСД нет {name}, гашение не отправлено.");

        var copy = new XElement(VetisNs.VetDocument + name);
        foreach (var partName in new[] { "businessEntity", "enterprise" })
        {
            var part = source.Elements().FirstOrDefault(element => element.Name.LocalName == partName);
            if (part == null)
                return Result.Failure<XElement>($"В ВСД у {name} нет {partName}, гашение не отправлено.");

            var identifiers = new XElement(part.Name);
            KeepGuid(new XElement(part), identifiers);
            if (!identifiers.Elements().Any(element => element.Name.LocalName is "uuid" or "guid"))
                return Result.Failure<XElement>($"В ВСД у {name}/{partName} нет uuid или guid, гашение не отправлено.");

            copy.Add(identifiers);
        }

        var gln = source.Elements().FirstOrDefault(element => element.Name.LocalName == "globalID");
        if (gln != null)
            copy.Add(new XElement(gln));

        return Result.Success(copy);
    }

    private static Result<XElement?> Waybill(XElement vetDocument)
    {
        var transport = vetDocument.Elements()
            .Where(element => element.Name.LocalName == "referencedDocument")
            .Where(element => Direct(element, "type") is "1" or "2" or "3" or "4" or "5")
            .ToList();
        var chosen = transport.FirstOrDefault(IsPrimaryRelation) ?? transport.FirstOrDefault();
        if (chosen == null)
            return Result.Success<XElement?>(null);

        var issueDate = Direct(chosen, "issueDate");
        if (string.IsNullOrWhiteSpace(issueDate))
            return Result.Failure<XElement?>("В ВСД есть связанный транспортный документ без даты, гашение не отправлено.");

        var waybill = new XElement(VetisNs.VetDocument + "waybill");
        var series = Direct(chosen, "issueSeries");
        if (!string.IsNullOrWhiteSpace(series))
            waybill.Add(new XElement(VetisNs.VetDocument + "issueSeries", series));

        var number = Direct(chosen, "issueNumber");
        if (!string.IsNullOrWhiteSpace(number))
            waybill.Add(new XElement(VetisNs.VetDocument + "issueNumber", number));

        waybill.Add(new XElement(VetisNs.VetDocument + "issueDate", issueDate));
        waybill.Add(new XElement(VetisNs.VetDocument + "type", Direct(chosen, "type")));
        return Result.Success<XElement?>(waybill);
    }

    private static bool IsPrimaryRelation(XElement element) => Direct(element, "relationshipType") == "1";

    private static XElement Inspection(string name, string login) =>
        new(VetisNs.VetDocument + name,
            new XElement(VetisNs.VetDocument + "responsible",
                new XElement(VetisNs.VetDocument + "login", login)),
            new XElement(VetisNs.VetDocument + "result", "CORRESPONDS"));

    private static void KeepGuid(XElement element)
    {
        if (element.Elements().Any(child => child.Name.LocalName is "uuid" or "guid"))
        {
            var chosen = element.Elements().FirstOrDefault(child => child.Name.LocalName == "guid")
                ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "uuid");
            element.RemoveNodes();
            if (chosen != null)
                element.Add(new XElement(chosen));
            return;
        }

        foreach (var child in element.Elements().ToList())
            KeepGuid(child);
    }

    /// <summary>
    /// Схема 2.1 принимает год, месяц и день в dictionary. Сервис 3.0 такие даты отклоняет.
    /// </summary>
    private static void RewriteDateParts(XElement date)
    {
        foreach (var part in date.Descendants().Where(element => element.Name.LocalName is "year" or "month" or "day" or "hour" or "minute").ToList())
            part.ReplaceWith(new XElement(VetisNs.Dictionary + part.Name.LocalName, part.Value));
    }

    private static void KeepGuid(XElement source, XElement target)
    {
        var chosen = source.Elements().FirstOrDefault(element => element.Name.LocalName == "guid")
            ?? source.Elements().FirstOrDefault(element => element.Name.LocalName == "uuid");
        if (chosen != null)
            target.Add(new XElement(chosen));
    }

    private static bool TryParse(string rawXml, out XElement document)
    {
        try
        {
            document = XElement.Parse(rawXml);
            return true;
        }
        catch (System.Xml.XmlException)
        {
            document = null!;
            return false;
        }
    }

    private static XElement? DirectElement(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName);

    private static string Direct(XElement parent, string localName) =>
        DirectElement(parent, localName)?.Value.Trim() ?? "";
}
