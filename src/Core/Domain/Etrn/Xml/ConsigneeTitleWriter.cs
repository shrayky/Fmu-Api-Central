using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Domain.Configuration.Constants;

namespace Domain.Etrn.Xml;

/// <summary>
/// Код СодОпПр в документах ОФД не задан, поэтому в атрибут пишется действие приёмки. Стенд поправит образец, если схема другая.
/// Подписанта и идентификатор участника ЭДО нет: подпись уходит отдельным методом.
/// </summary>
public static class ConsigneeTitleWriter
{
    public const string Knd = "1110341";

    public static string Build(EtrnIncomingDocumentEntity document, EtrnDecision decision) =>
        Build(document, decision, DateTimeOffset.Now);

    public static string Build(EtrnIncomingDocumentEntity document, EtrnDecision decision, DateTimeOffset signedAt)
    {
        var local = signedAt.DateTime;
        var body = new XElement("СодИнфГП");
        Add(body, "УИД_ТрН", document.Uid);
        Add(body, "НомДок", document.Number);
        var date = WaybillDate(document.Date);
        if (date.Length > 0)
            body.Add(new XAttribute("ДатаДок", date));
        body.Add(new XAttribute("СодОпПр", decision.Action ?? ""));

        if (decision.Action == EtrnAcceptanceAction.Partially)
        {
            foreach (var line in decision.Lines)
                body.Add(Line(line));
        }
        else if (decision.Action == EtrnAcceptanceAction.Rejected && decision.GeneralComment.Length > 0)
        {
            body.Add(new XElement("Комментарий", decision.GeneralComment));
        }

        var title = new XElement(
            "Документ",
            new XAttribute("КНД", Knd),
            new XAttribute("ПоФактХЖ", "Транспортная накладная, информация грузополучателя"),
            new XAttribute("ДатИнфГП", local.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)),
            new XAttribute("ВрИнфГП", local.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
            body);

        var file = new XElement(
            "Файл",
            new XAttribute("ВерсФорм", "5.01"),
            new XAttribute("ВерсПрог", $"{ApplicationInformation.Name}/{ApplicationInformation.Version}.{ApplicationInformation.Assembly}"),
            title);

        return new XDocument(file).ToString(SaveOptions.DisableFormatting);
    }

    public static byte[] ToUtf8(string xml) => Encoding.UTF8.GetBytes(xml);

    private static XElement Line(EtrnDecidedLine line)
    {
        var element = new XElement(
            "ПриемГруза",
            new XAttribute("НомСтр", line.Index.ToString(CultureInfo.InvariantCulture)));
        if (line.FactPlaces is int places)
            element.Add(new XAttribute("КолМест", places.ToString(CultureInfo.InvariantCulture)));
        if (!string.IsNullOrWhiteSpace(line.FactWeight))
            element.Add(new XAttribute("МассаБрутто", line.FactWeight.Trim()));
        if (!string.IsNullOrWhiteSpace(line.Comment))
            element.Add(new XAttribute("Комментарий", line.Comment.Trim()));
        return element;
    }

    private static void Add(XElement element, string name, string? value)
    {
        var text = (value ?? "").Trim();
        if (text.Length > 0)
            element.Add(new XAttribute(name, text));
    }

    private static string WaybillDate(string? date)
    {
        var text = (date ?? "").Trim();
        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : text;
    }
}
