using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Domain.Etrn;

namespace EtrnSaby.Xml;

/// <summary>
/// Титул грузополучателя КНД 1110341. Имя файла, участники ЭДО и подписант в XML не пишутся.
/// ПриемГрузГП обязателен при приёмке: без него СБИС отклоняет ВыполнитьДействие.
/// Время выгрузки — момент подписи: отдельных полей прибытия и убытия на карточке нет.
/// Масса с кодом 03: взвешивания нет, берём массу из титула.
/// </summary>
public static class ReceiverTitleWriter
{
    public const string Knd = "1110341";

    public const string DefaultFileName = "ON_TRNACLGRPO.xml";

    private const string Accepted =
        "Лицом, управомоченным на получение груза, при указанных обстоятельствах принят от перевозчика груз с указанными характеристиками";

    private const string Refused =
        "Лицом, управомоченным на получение груза, при указанных обстоятельствах отказано в принятии от перевозчика груза с указанными характеристиками";

    public static string Build(EtrnIncomingDocumentEntity document, EtrnDecision decision) =>
        Build(document, decision, DateTimeOffset.Now);

    public static string Build(EtrnIncomingDocumentEntity document, EtrnDecision decision, DateTimeOffset signedAt)
    {
        var body = new XElement("СодИнфГП", new XAttribute("УИД_ТрН", Uid(document.Uid)));
        if (decision.Action == EtrnAcceptanceAction.Rejected)
        {
            body.Add(new XAttribute("СодОпОтк", Refused));
            body.Add(new XAttribute("ПричОтк", Reason(decision.GeneralComment)));
        }
        else
        {
            body.Add(new XAttribute("СодОпПр", Accepted));
            body.Add(Acceptance(document, decision, Moment(signedAt)));
        }

        var title = new XElement(
            "Документ",
            new XAttribute("КНД", Knd),
            new XAttribute("ПоФактХЖ", "Транспортная накладная, информация грузополучателя о приеме груза"),
            new XAttribute("ДатИнфГП", signedAt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)),
            new XAttribute("ВрИнфГП", signedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
            body);

        var file = new XElement(
            "Файл",
            new XAttribute("ВерсФорм", "5.01"),
            new XAttribute("ВерсПрог", SabyUserAgent.Value),
            title);

        return new XDocument(new XDeclaration("1.0", "utf-8", null), file)
            .ToString(SaveOptions.DisableFormatting);
    }

    public static byte[] ToUtf8(string xml) => Encoding.UTF8.GetBytes(xml);

    private static XElement Acceptance(EtrnIncomingDocumentEntity document, EtrnDecision decision, string moment)
    {
        var partial = decision.Action == EtrnAcceptanceAction.Partially;
        var places = partial
            ? Sum(decision.Lines.Select(line => line.FactPlaces))
            : Sum(document.Lines.Select(line => line.Places));
        var weight = partial
            ? Weight(decision.Lines.Select(line => line.FactWeight))
            : Weight(document.Lines.Select(line => line.GrossWeight));

        var element = new XElement(
            "ПриемГрузГП",
            new XAttribute("ФДатВрПриб", moment),
            new XAttribute("НалКоорТочВрФПр", "1"),
            new XAttribute("ФДатВрУбыт", moment),
            new XAttribute("НалКоорТочВрФУб", "1"),
            new XAttribute("ЗаявДатВрПриб", moment),
            new XAttribute("НалКоорТочВрЗПр", "1"),
            new XAttribute("МетОпрМасс", "03"),
            new XAttribute("КолМестПриемЧ", places.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("ОбщСвСост", Condition(decision)),
            new XAttribute("МасБрутЗначПрием", weight));

        var address = (document.DeliveryAddress ?? "").Trim();
        if (address.Length > 0)
        {
            element.Add(new XElement(
                "АдрВыгруз",
                new XElement(
                    "АдресИнф",
                    new XAttribute("КодСтр", "643"),
                    new XAttribute("АдрТекст", address))));
        }

        return element;
    }

    private static string Condition(EtrnDecision decision)
    {
        if (decision.Action != EtrnAcceptanceAction.Partially)
            return "груз принят";

        var notes = decision.Lines
            .Select(line => (line.Comment ?? "").Trim())
            .Where(comment => comment.Length > 0);
        var text = string.Join("; ", notes);
        return text.Length > 0 ? text : "груз принят частично";
    }

    private static int Sum(IEnumerable<int?> values)
    {
        var sum = 0;
        foreach (var value in values)
            sum += value ?? 0;
        return sum;
    }

    private static string Weight(IEnumerable<string?> values)
    {
        decimal sum = 0;
        foreach (var value in values)
        {
            if (EtrnQuantity.TryWeight(value, out var parsed))
                sum += parsed;
        }

        return sum.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Uid(string? uid)
    {
        var text = (uid ?? "").Trim();
        return text.Length > 0 ? text : "Без номера";
    }

    private static string Reason(string? comment)
    {
        var text = (comment ?? "").Trim();
        return text.Length > 0 ? text : "груз не принят";
    }

    private static string Moment(DateTimeOffset at) =>
        at.ToString("dd.MM.yyyy'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
}
