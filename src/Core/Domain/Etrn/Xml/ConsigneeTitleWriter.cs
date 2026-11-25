using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Domain.Configuration.Constants;

namespace Domain.Etrn.Xml;

/// <summary>
/// СодОпПр и СодОпОтк — фиксированные фразы формата: Контур отклоняет «Получен».
/// ПриемГрузГП обязателен при приёме. Время выгрузки — момент подписи, отдельных полей прибытия нет.
/// Масса с кодом 03: взвешивания нет, берём массу из титула.
/// ИспрТрН пишется только у повторной отправки: номер и дату вводят на карточке.
/// Подписант — ФИО сертификата и СтатПодп 1: доверенности в настройках нет.
/// Идентификатора участника ЭДО нет: подпись уходит отдельным методом.
/// </summary>
public static class ConsigneeTitleWriter
{
    public const string Knd = "1110341";

    private const string Accepted =
        "Лицом, управомоченным на получение груза, при указанных обстоятельствах принят от перевозчика груз с указанными характеристиками";

    private const string Refused =
        "Лицом, управомоченным на получение груза, при указанных обстоятельствах отказано в принятии от перевозчика груза с указанными характеристиками";

    private static readonly Encoding Windows1251 = CreateWindows1251();

    public static string Build(EtrnIncomingDocumentEntity document, EtrnDecision decision) =>
        Build(document, decision, DateTimeOffset.Now);

    public static string Build(
        EtrnIncomingDocumentEntity document,
        EtrnDecision decision,
        DateTimeOffset signedAt,
        EtrnSignerName? signer = null,
        EtrnCorrection? correction = null)
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
            body.Add(new XAttribute("СодОпОтк", Refused));
            body.Add(new XAttribute("ПричОтк", Reason(decision.GeneralComment)));
            AddCorrection(body, correction);
        }
        else
        {
            body.Add(new XAttribute("СодОпПр", Accepted));
            AddCorrection(body, correction);
            body.Add(Acceptance(document, decision, signedAt));
        }

        var title = new XElement(
            "Документ",
            new XAttribute("КНД", Knd),
            new XAttribute("ПоФактХЖ", "Транспортная накладная, информация грузополучателя"),
            new XAttribute("ДатИнфГП", local.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)),
            new XAttribute("ВрИнфГП", local.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
            body);
        AddSigner(title, signer);

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

    private static void AddCorrection(XElement body, EtrnCorrection? correction)
    {
        if (correction == null)
            return;

        body.Add(new XElement(
            "ИспрТрН",
            new XAttribute("НомИспр", correction.Number.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("ДатаИспр", correction.Date)));
    }

    private static void AddSigner(XElement title, EtrnSignerName? signer)
    {
        var surname = (signer?.Surname ?? "").Trim();
        var name = (signer?.Name ?? "").Trim();
        if (surname.Length == 0 || name.Length == 0)
            return;

        var person = new XElement(
            "ФИО",
            new XAttribute("Фамилия", surname),
            new XAttribute("Имя", name));
        var patronymic = (signer?.Patronymic ?? "").Trim();
        if (patronymic.Length > 0)
            person.Add(new XAttribute("Отчество", patronymic));

        title.Add(new XElement("Подписант", new XAttribute("СтатПодп", "1"), person));
    }

    private static XElement Acceptance(EtrnIncomingDocumentEntity document, EtrnDecision decision, DateTimeOffset signedAt)
    {
        var partial = decision.Action == EtrnAcceptanceAction.Partially;
        var places = partial
            ? Sum(decision.Lines.Select(line => line.FactPlaces))
            : Sum((document.Lines ?? []).Select(line => line.Places));
        var weight = partial
            ? Weight(decision.Lines.Select(line => line.FactWeight))
            : Weight((document.Lines ?? []).Select(line => line.GrossWeight));
        var moment = signedAt.ToString("dd.MM.yyyy'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

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

        var notes = new List<string>();
        var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in decision.Lines)
        {
            var comment = (line.Comment ?? "").Trim();
            if (comment.Length == 0 || !written.Add(comment))
                continue;

            notes.Add(comment);
        }

        return notes.Count > 0 ? string.Join("; ", notes) : "груз принят частично";
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

    private static string Reason(string? comment)
    {
        var text = (comment ?? "").Trim();
        return text.Length > 0 ? text : "груз не принят";
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
