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
    public void Полный_приём_не_пишет_факты_строк()
    {
        var xml = ConsigneeTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Received
        }, SignedAt);

        Assert.Contains("ВерсФорм=\"5.01\"", xml);
        Assert.Contains("КНД=\"1110341\"", xml);
        Assert.Contains("СодОпПр=\"Получен\"", xml);
        Assert.DoesNotContain("ПричОтк", xml);
        Assert.DoesNotContain("ПриемГруза", xml);
        Assert.DoesNotContain("ПриемГрузГП", xml);
        Assert.DoesNotContain("МассаБрутто", xml);
        Assert.DoesNotContain("ОтметГП", xml);
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

    [Theory]
    [InlineData("Подписант")]
    [InlineData("ИдФайл")]
    [InlineData("ИдентификаторУчастника")]
    public void В_титуле_нет_подписанта_и_участника_ЭДО(string forbidden)
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
