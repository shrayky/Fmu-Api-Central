using System.Text;
using System.Xml.Linq;
using Domain.Configuration.Constants;
using Domain.Etrn;
using Domain.Etrn.Xml;

namespace Domain.Tests;

public class ConsigneeTitleWriterTests
{
    private static readonly DateTimeOffset SignedAt = new(2026, 11, 10, 13, 5, 0, TimeSpan.FromHours(7));

    private static readonly EtrnIncomingDocumentEntity Document = new()
    {
        Number = "ТН-77",
        Date = "2026-11-04",
        Uid = "uid-1",
        DeliveryAddress = "Красноярск, Апрельская, 7",
        Lines =
        [
            new EtrnCargoLine { Index = 1, Name = "Сыр", Places = 10, GrossWeight = "100" },
            new EtrnCargoLine { Index = 2, Name = "Масло", Places = 5, GrossWeight = "50" }
        ]
    };

    [Fact]
    public void Частичная_приёмка_совпадает_с_образцом()
    {
        var xml = ConsigneeTitleWriter.Build(Document, Partial(), SignedAt);

        Assert.Equal(Canonical(Sample("consignee-partial.xml")), Canonical(xml));
    }

    [Fact]
    public void Полный_приём_пишет_формулировку_формата_и_сведения_о_приёме()
    {
        var xml = ConsigneeTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Received
        }, SignedAt);

        const string accepted =
            "Лицом, управомоченным на получение груза, при указанных обстоятельствах принят от перевозчика груз с указанными характеристиками";
        var body = XDocument.Parse(xml).Descendants("СодИнфГП").Single();
        Assert.Equal(accepted, body.Attribute("СодОпПр")?.Value);
        Assert.DoesNotContain("СодОпПр=\"Получен\"", xml, StringComparison.Ordinal);
        var cargo = Assert.Single(body.Elements("ПриемГрузГП"));
        Assert.Equal("10.11.2026T13:05:00+07:00", cargo.Attribute("ФДатВрПриб")?.Value);
        Assert.Equal("1", cargo.Attribute("НалКоорТочВрФПр")?.Value);
        Assert.Equal("10.11.2026T13:05:00+07:00", cargo.Attribute("ФДатВрУбыт")?.Value);
        Assert.Equal("1", cargo.Attribute("НалКоорТочВрФУб")?.Value);
        Assert.Equal("10.11.2026T13:05:00+07:00", cargo.Attribute("ЗаявДатВрПриб")?.Value);
        Assert.Equal("1", cargo.Attribute("НалКоорТочВрЗПр")?.Value);
        Assert.Equal("03", cargo.Attribute("МетОпрМасс")?.Value);
        Assert.Equal("15", cargo.Attribute("КолМестПриемЧ")?.Value);
        Assert.Equal("150", cargo.Attribute("МасБрутЗначПрием")?.Value);
        Assert.Equal("груз принят", cargo.Attribute("ОбщСвСост")?.Value);
        Assert.Equal("643", cargo.Element("АдрВыгруз")?.Element("АдресИнф")?.Attribute("КодСтр")?.Value);
        Assert.Equal("Красноярск, Апрельская, 7", cargo.Element("АдрВыгруз")?.Element("АдресИнф")?.Attribute("АдрТекст")?.Value);
        Assert.Empty(cargo.Elements("СвПринПоНаим"));
        Assert.Empty(body.Elements("ОтметГП"));
        Assert.Null(body.Attribute("ПричОтк"));
    }

    [Fact]
    public void Отказ_пишет_общий_комментарий_без_фактов()
    {
        var xml = ConsigneeTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Rejected,
            GeneralComment = "повреждён груз",
            Lines = [new EtrnDecidedLine { Index = 1, FactPlaces = 0, FactWeight = "0", Comment = "не писать" }]
        }, SignedAt);

        Assert.DoesNotContain("СодОпПр", xml);
        Assert.Contains(
            "СодОпОтк=\"Лицом, управомоченным на получение груза, при указанных обстоятельствах отказано в принятии от перевозчика груза с указанными характеристиками\"",
            xml);
        Assert.Contains("ПричОтк=\"повреждён груз\"", xml);
        Assert.DoesNotContain("ПриемГруза", xml);
        Assert.DoesNotContain("ПриемГрузГП", xml);
        Assert.DoesNotContain("ОтметГП", xml);
        Assert.DoesNotContain("не писать", xml);
    }

    [Fact]
    public void Тело_для_платформы_в_windows_1251()
    {
        var xml = ConsigneeTitleWriter.Build(Document, Partial(), SignedAt);
        var bytes = ConsigneeTitleWriter.ToWindows1251(xml);
        var text = Encoding.GetEncoding("windows-1251").GetString(bytes);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"windows-1251\"?>", text, StringComparison.Ordinal);
        Assert.Contains("СодОпПр", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Исправление_пишет_номер_и_дату_перед_приёмом()
    {
        var xml = ConsigneeTitleWriter.Build(
            Document,
            new EtrnDecision { Action = EtrnAcceptanceAction.Received },
            SignedAt,
            correction: new EtrnCorrection { Number = 1, Date = "20.11.2026" });

        var body = XDocument.Parse(xml).Descendants("СодИнфГП").Single();
        var correction = body.Element("ИспрТрН");
        Assert.NotNull(correction);
        Assert.Equal("1", correction.Attribute("НомИспр")?.Value);
        Assert.Equal("20.11.2026", correction.Attribute("ДатаИспр")?.Value);
        Assert.Equal("ПриемГрузГП", correction.ElementsAfterSelf().First().Name.LocalName);
    }

    [Fact]
    public void Исправление_отказа_пишет_номер_без_приёма()
    {
        var xml = ConsigneeTitleWriter.Build(
            Document,
            new EtrnDecision { Action = EtrnAcceptanceAction.Rejected, GeneralComment = "брак" },
            SignedAt,
            correction: new EtrnCorrection { Number = 2, Date = "21.11.2026" });

        var body = XDocument.Parse(xml).Descendants("СодИнфГП").Single();
        var correction = body.Element("ИспрТрН");
        Assert.NotNull(correction);
        Assert.Equal("2", correction.Attribute("НомИспр")?.Value);
        Assert.Empty(body.Elements("ПриемГрузГП"));
        Assert.NotNull(body.Attribute("СодОпОтк"));
    }

    [Fact]
    public void Первая_приёмка_не_пишет_исправление()
    {
        var xml = ConsigneeTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Received
        }, SignedAt);

        Assert.DoesNotContain("ИспрТрН", xml);
    }

    [Fact]
    public void Приёмка_пишет_подписанта_из_сертификата()
    {
        var xml = ConsigneeTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Received
        }, SignedAt, new EtrnSignerName
        {
            Surname = "Большаков",
            Name = "Сергей",
            Patronymic = "Александрович"
        });

        var signer = XDocument.Parse(xml).Descendants("Подписант").Single();
        Assert.Equal("1", signer.Attribute("СтатПодп")?.Value);
        Assert.Equal("СодИнфГП", signer.ElementsBeforeSelf().Single().Name.LocalName);
        var fio = Assert.Single(signer.Elements());
        Assert.Equal("ФИО", fio.Name.LocalName);
        Assert.Equal("Большаков", fio.Attribute("Фамилия")?.Value);
        Assert.Equal("Сергей", fio.Attribute("Имя")?.Value);
        Assert.Equal("Александрович", fio.Attribute("Отчество")?.Value);
    }

    [Fact]
    public void Подписант_без_отчества_не_пишет_пустой_атрибут()
    {
        var xml = ConsigneeTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Received
        }, SignedAt, new EtrnSignerName
        {
            Surname = "Большаков",
            Name = "Сергей"
        });

        var fio = XDocument.Parse(xml).Descendants("ФИО").Single();
        Assert.Null(fio.Attribute("Отчество"));
        Assert.Equal("Сергей", fio.Attribute("Имя")?.Value);
    }

    [Theory]
    [InlineData("ИдФайл")]
    [InlineData("ИдентификаторУчастника")]
    public void В_титуле_нет_участника_ЭДО(string forbidden)
    {
        var xml = ConsigneeTitleWriter.Build(Document, Partial(), SignedAt);

        Assert.DoesNotContain(forbidden, xml);
        Assert.Contains($"{ApplicationInformation.Name}/{ApplicationInformation.Version}.{ApplicationInformation.Assembly}", xml);
    }

    private static EtrnDecision Partial() => new()
    {
        Action = EtrnAcceptanceAction.Partially,
        Lines =
        [
            new EtrnDecidedLine { Index = 1, FactPlaces = 9, FactWeight = "99.5", Comment = "недовоз" },
            new EtrnDecidedLine { Index = 2, FactPlaces = 5, FactWeight = "50", Comment = "общий" }
        ]
    };

    private static string Sample(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "XmlSamples", name));

    private static string Canonical(string xml) =>
        XDocument.Parse(xml).ToString(SaveOptions.DisableFormatting);
}
