using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Domain.Etrn;

namespace EtrnSaby.Xml;

/// <summary>
/// Титул грузополучателя КНД 1110341. Имя файла, участники ЭДО и подписант в XML не пишутся.
/// </summary>
public static class ReceiverTitleWriter
{
    public const string Knd = "1110341";

    public const string DefaultFileName = "ON_TRNACLGRPO.xml";

    public static string Build(EtrnIncomingDocumentEntity document, EtrnDecision decision)
    {
        var title = new XElement(
            "Документ",
            new XAttribute("КНД", Knd),
            new XAttribute("НаимДок", "Транспортная накладная (информация грузополучателя)"),
            new XAttribute("Действие", decision.Action),
            new XAttribute("НомДок", document.Number ?? ""),
            new XAttribute("ДатаДок", RussianDate(document.Date)));

        if (!string.IsNullOrWhiteSpace(document.Uid))
            title.Add(new XAttribute("УИД_ЕТД", document.Uid.Trim()));

        if (decision.Action == EtrnAcceptanceAction.Partially)
            title.Add(new XElement("СвГП", decision.Lines.Select(Line)));

        if (decision.Action == EtrnAcceptanceAction.Rejected && decision.GeneralComment.Length > 0)
            title.Add(new XElement("Комментарий", decision.GeneralComment));

        var file = new XElement(
            "Файл",
            new XAttribute("ВерсФорм", "5.01"),
            new XAttribute("ВерсПрог", SabyUserAgent.Value),
            title);

        var text = new XDocument(new XDeclaration("1.0", "utf-8", null), file)
            .ToString(SaveOptions.DisableFormatting);
        return text;
    }

    public static byte[] ToUtf8(string xml) => Encoding.UTF8.GetBytes(xml);

    private static XElement Line(EtrnDecidedLine line)
    {
        var element = new XElement("Факт", new XAttribute("НомСтр", line.Index.ToString(CultureInfo.InvariantCulture)));
        if (line.FactPlaces is int places)
            element.Add(new XAttribute("КолМест", places.ToString(CultureInfo.InvariantCulture)));
        if (!string.IsNullOrWhiteSpace(line.FactWeight))
            element.Add(new XAttribute("ВесБрутто", line.FactWeight.Trim()));
        if (!string.IsNullOrWhiteSpace(line.Comment))
            element.Add(new XAttribute("Комментарий", line.Comment.Trim()));
        return element;
    }

    private static string RussianDate(string? date)
    {
        var text = (date ?? "").Trim();
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return text;
    }
}
