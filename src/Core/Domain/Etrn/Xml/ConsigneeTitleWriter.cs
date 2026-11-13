using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Domain.Configuration.Constants;

namespace Domain.Etrn.Xml;

/// <summary>
/// Отказ пишется атрибутом ПричОтк, приём — атрибутом СодОпПр. Факты частичной приёмки идут узлами
/// СодИнфГП/ПриемГрузГП/СвПринПоНаим, уникальные комментарии — отдельными ОтметГП/@ОбстОтмет.
/// Подписанта и идентификатора участника ЭДО нет: подпись уходит отдельным методом.
/// </summary>
public static class ConsigneeTitleWriter
{
    public const string Knd = "1110341";

    private static readonly Encoding Windows1251 = CreateWindows1251();

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

        if (decision.Action == EtrnAcceptanceAction.Rejected)
        {
            Add(body, "ПричОтк", decision.GeneralComment);
        }
        else
        {
            body.Add(new XAttribute("СодОпПр", decision.Action ?? ""));
        }

        if (decision.Action == EtrnAcceptanceAction.Partially)
        {
            AddFacts(body, document, decision);
            AddComments(body, decision);
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

    /// <summary>
    /// Платформа ЭТрН принимает титул в windows-1251 с объявлением кодировки.
    /// </summary>
    public static byte[] ToWindows1251(string xml) =>
        Windows1251.GetBytes("<?xml version=\"1.0\" encoding=\"windows-1251\"?>" + xml);

    private static Encoding CreateWindows1251()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding("windows-1251");
    }

    private static void AddFacts(XElement body, EtrnIncomingDocumentEntity document, EtrnDecision decision)
    {
        var names = (document.Lines ?? [])
            .GroupBy(line => line.Index)
            .ToDictionary(group => group.Key, group => group.Last().Name);

        var facts = new XElement("ПриемГрузГП");
        foreach (var line in decision.Lines)
        {
            var fact = new XElement("СвПринПоНаим");
            if (names.TryGetValue(line.Index, out var name) && !string.IsNullOrWhiteSpace(name))
                fact.Add(new XAttribute("НаимГруз", name.Trim()));
            if (line.FactPlaces is int places)
                fact.Add(new XAttribute("КолМестГр", places.ToString(CultureInfo.InvariantCulture)));
            if (!string.IsNullOrWhiteSpace(line.FactWeight))
                fact.Add(new XAttribute("МасБрутЗнач", line.FactWeight.Trim()));
            facts.Add(fact);
        }

        body.Add(facts);
    }

    private static void AddComments(XElement body, EtrnDecision decision)
    {
        var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in decision.Lines)
        {
            var comment = (line.Comment ?? "").Trim();
            if (comment.Length == 0 || !written.Add(comment))
                continue;

            body.Add(new XElement("ОтметГП", new XAttribute("ОбстОтмет", comment)));
        }
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
