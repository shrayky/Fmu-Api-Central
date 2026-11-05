using System.Globalization;
using System.Text;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using Domain.Etrn;

namespace EtrnSaby.Xml;

public sealed class SenderTitle
{
    public string Number { get; init; } = "";

    public string Date { get; init; } = "";

    public string Uid { get; init; } = "";

    public string Consignor { get; init; } = "";

    public string Address { get; init; } = "";

    public List<EtrnCargoLine> Lines { get; init; } = [];

    public string RawXml { get; init; } = "";

    public EtrnFetchedDocument ToFetched(string operatorDocumentId) => new()
    {
        OperatorDocumentId = operatorDocumentId,
        Number = Number,
        Date = Date,
        Consignor = Consignor,
        Uid = Uid,
        DeliveryAddress = Address,
        Lines = Lines,
        RawXml = RawXml
    };
}

public static class SenderTitleReader
{
    public const string NotParsed = "Титул грузоотправителя не разобран";

    private const string Knd = "1110339";

    private static readonly string[] AddressNames =
    [
        "АдрПунктНазн", "АдресВыгрузки", "АдрМестаВыгрузки", "ПунктВыгрузки", "АдресПунктаНазначения"
    ];

    private static readonly string[] AddressTextNames =
    [
        "ТекстАдрес", "АдрТекст", "АдресТекст", "АдресСтрокой"
    ];

    private static readonly string[] LineNames =
    [
        "ОпГруз", "СтрокаГруза", "СтрокаНакладной", "Груз"
    ];

    static SenderTitleReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Result<SenderTitle> Read(byte[] xml)
    {
        XDocument document;
        try
        {
            using var stream = new MemoryStream(xml);
            document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            return Result.Failure<SenderTitle>($"{NotParsed}: {ex.Message}");
        }

        var root = document.Root;
        if (root == null || !string.Equals(root.Name.LocalName, "Файл", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<SenderTitle>(NotParsed);

        var title = FindTitle(root);
        if (title == null || !KndMatches(title))
            return Result.Failure<SenderTitle>(NotParsed);

        return Result.Success(new SenderTitle
        {
            Number = AnyAttribute(title, "НомДок", "НомерДок", "Номер", "НомДок") ?? "",
            Date = NormalizeDate(AnyAttribute(title, "ДатаДок", "ДатаДокумента", "Дата")),
            Uid = AnyAttribute(title, "УИД_ЕТД", "УИД", "ИдГИСЭПД", "ИдентификаторГИСЭПД") ?? "",
            Consignor = Consignor(root, title),
            Address = Address(title),
            Lines = Lines(title),
            RawXml = Decode(xml)
        });
    }

    private static XElement? FindTitle(XElement root)
    {
        if (string.Equals(root.Name.LocalName, "Документ", StringComparison.OrdinalIgnoreCase))
            return root;

        return root.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "Документ", StringComparison.OrdinalIgnoreCase));
    }

    private static bool KndMatches(XElement title)
    {
        var knd = (Attribute(title, "КНД") ?? "").Trim();
        return knd.Length == 0 || knd == Knd;
    }

    private static string Consignor(XElement root, XElement title)
    {
        var containers = new[] { "СвГО", "СвГрузОт", "Грузоотправитель" };
        foreach (var element in root.Descendants())
        {
            if (!containers.Contains(element.Name.LocalName, StringComparer.OrdinalIgnoreCase))
                continue;

            var name = element.DescendantsAndSelf()
                .Select(item => Attribute(item, "НаимОрг", "НаименованиеОрг", "Наименование", "НаимСокр")
                                ?? Value(item, "НаимОрг", "НаименованиеОрг", "Наименование", "НаимСокр"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(name))
                return name.Trim();
        }

        return (Attribute(title, "Грузоотправитель", "НаимГО") ?? "").Trim();
    }

    private static string Address(XElement title)
    {
        var points = title.Descendants()
            .Where(element => AddressNames.Contains(element.Name.LocalName, StringComparer.OrdinalIgnoreCase))
            .Where(element => !element.Ancestors().Any(ancestor =>
                AddressNames.Contains(ancestor.Name.LocalName, StringComparer.OrdinalIgnoreCase)))
            .Select(FormatAddress)
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (points.Count > 0)
            return string.Join("; ", points);

        var direct = Attribute(title, "АдресВыгрузки", "АдрПунктНазн");
        return (direct ?? "").Trim();
    }

    private static string FormatAddress(XElement point)
    {
        var nested = point.Elements().FirstOrDefault(element =>
            string.Equals(element.Name.LocalName, "Адрес", StringComparison.OrdinalIgnoreCase)
            || AddressNames.Contains(element.Name.LocalName, StringComparer.OrdinalIgnoreCase));
        if (nested != null)
        {
            var text = FormatAddress(nested);
            if (text.Length > 0)
                return text;
        }

        foreach (var name in AddressTextNames)
        {
            var text = Attribute(point, name)
                       ?? Value(point, name)
                       ?? Attribute(point, "Адрес");
            if (!string.IsNullOrWhiteSpace(text))
                return Normalize(text);
        }

        var parts = new List<string>();
        Add(parts, point, "Индекс");
        Add(parts, point, "НаимРегион", "Регион", "КодРегион");
        Add(parts, point, "Город");
        Add(parts, point, "НаселПункт", "НаселенныйПункт");
        Add(parts, point, "Улица");
        Add(parts, point, "Дом");
        Add(parts, point, "Корпус");
        if (parts.Count > 0)
            return string.Join(", ", parts);

        return Normalize(point.Value);
    }

    private static void Add(List<string> parts, XElement element, params string[] names)
    {
        var value = names
            .Select(name => Attribute(element, name) ?? Value(element, name))
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add(value.Trim());
    }

    private static List<EtrnCargoLine> Lines(XElement title)
    {
        var lines = title.Descendants()
            .Where(element => LineNames.Contains(element.Name.LocalName, StringComparer.OrdinalIgnoreCase))
            .Where(element => !element.Descendants().Any(child =>
                LineNames.Contains(child.Name.LocalName, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var result = new List<EtrnCargoLine>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var element = lines[index];
            var places = ParsePlaces(Attribute(element, "КолМестГр", "КолМест", "КоличествоМест", "КолМестФакт"));
            result.Add(new EtrnCargoLine
            {
                Index = ParseIndex(Attribute(element, "НомерГруз", "Номер", "НомПоряд"), index + 1),
                Name = (Attribute(element, "НаимГруз", "НаименованиеГруза", "Наименование", "Название") ?? "").Trim(),
                Places = places,
                GrossWeight = NormalizeWeight(Attribute(element, "ВесГруз", "МассаБрутто", "ВесБрутто", "Масса"))
            });
        }

        return result;
    }

    private static int ParseIndex(string? value, int fallback) =>
        int.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            ? index
            : fallback;

    private static int? ParsePlaces(string? value) =>
        int.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var places) && places >= 0
            ? places
            : null;

    private static string NormalizeWeight(string? value)
    {
        var text = (value ?? "").Trim().Replace(',', '.');
        if (text.Length == 0)
            return "";

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var weight)
            ? weight.ToString(CultureInfo.InvariantCulture)
            : "";
    }

    private static string NormalizeDate(string? value)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0)
            return "";

        foreach (var format in new[] { "dd.MM.yyyy", "yyyy-MM-dd", "dd.MM.yyyy HH:mm:ss" })
        {
            if (DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return text.Length >= 10 ? text[..10] : text;
    }

    private static string? Attribute(XElement element, params string[] names) =>
        names.Select(name => element.Attribute(name)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    /// <summary>
    /// Номер, дата и УИД лежат на вложенном СодИнфГО, а не на самом Документе.
    /// </summary>
    private static string? AnyAttribute(XElement title, params string[] names) =>
        Attribute(title, names)
        ?? title.Descendants()
            .Select(element => Attribute(element, names))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? Value(XElement element, params string[] names) =>
        element.Elements()
            .Where(child => names.Contains(child.Name.LocalName, StringComparer.OrdinalIgnoreCase))
            .Select(child => child.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Кодировка из объявления: windows-1251 не должна превратиться в мусор.
    /// </summary>
    private static string Decode(byte[] xml)
    {
        var encoding = Encoding.UTF8;
        var head = Encoding.ASCII.GetString(xml, 0, Math.Min(xml.Length, 200));
        var declared = head.IndexOf("encoding", StringComparison.OrdinalIgnoreCase);
        if (declared >= 0)
        {
            var quote = head.IndexOfAny(['"', '\''], declared);
            if (quote >= 0)
            {
                var end = head.IndexOf(head[quote], quote + 1);
                if (end > quote)
                {
                    try
                    {
                        encoding = Encoding.GetEncoding(head[(quote + 1)..end]);
                    }
                    catch (ArgumentException)
                    {
                        encoding = Encoding.UTF8;
                    }
                }
            }
        }

        return encoding.GetString(xml).TrimStart('\uFEFF');
    }
}
