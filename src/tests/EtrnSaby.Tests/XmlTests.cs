using System.Text;
using Domain.Etrn;
using EtrnSaby.Xml;

namespace EtrnSaby.Tests;

public class SenderTitleReaderTests
{
    static SenderTitleReaderTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void Читает_номер_дату_УИД_адрес_и_строку()
    {
        var title = SenderTitleReader.Read(Sample("sender-simple.xml"));

        Assert.True(title.IsSuccess);
        var value = title.Value;
        Assert.Equal("ТН-77", value.Number);
        Assert.Equal("2026-11-04", value.Date);
        Assert.Equal("a1b2c3d4-0000-1111-2222-333344445555", value.Uid);
        Assert.Equal("ООО Поставщик", value.Consignor);
        Assert.Equal("630000, Новосибирская область, Новосибирск, Новосибирск, Ленина, 1, 2", value.Address);
        var line = Assert.Single(value.Lines);
        Assert.Equal(1, line.Index);
        Assert.Equal("Ящики", line.Name);
        Assert.Equal(10, line.Places);
        Assert.Equal("100.000", line.GrossWeight);
    }

    [Fact]
    public void Несколько_адресов_склеиваются_через_точку_с_запятой()
    {
        var title = SenderTitleReader.Read(Sample("sender-multiple-addresses.xml"));

        Assert.True(title.IsSuccess);
        Assert.Equal("630000, Новосибирск, Ленина, 1; г. Томск, ул. Мира, 5", title.Value.Address);
    }

    [Fact]
    public void Строка_без_массы_и_строка_без_количества()
    {
        var title = SenderTitleReader.Read(Sample("sender-multiple-addresses.xml"));

        Assert.True(title.IsSuccess);
        Assert.Equal(2, title.Value.Lines.Count);
        Assert.True(title.Value.Lines[0].HasQuantity);
        Assert.False(title.Value.Lines[0].HasWeight);
        Assert.False(title.Value.Lines[1].HasQuantity);
        Assert.Null(title.Value.Lines[1].Places);
        Assert.Equal("", title.Value.Lines[1].GrossWeight);
    }

    [Fact]
    public void Чужой_корень_не_разобран()
    {
        var title = SenderTitleReader.Read(Sample("sender-foreign-root.xml"));

        Assert.True(title.IsFailure);
        Assert.Equal(SenderTitleReader.NotParsed, title.Error);
    }

    [Fact]
    public void Кодировка_windows_1251_соблюдается()
    {
        var xml = """
                  <?xml version="1.0" encoding="windows-1251"?>
                  <Файл ИдФайл="ON_TRNACLGROT_1" ВерсПрог="1С" ВерсФорм="5.01">
                    <Документ КНД="1110339">
                      <СодИнфГО НомДок="ТН-1" ДатаДок="04.11.2026">
                        <СвГО><РекИдентГО НаимОрг="ООО Ромашка" /></СвГО>
                        <СвГруз><ОпГруз НаимГруз="Ящики" КолМестГр="2" ВесГруз="5" /></СвГруз>
                      </СодИнфГО>
                    </Документ>
                  </Файл>
                  """;
        var bytes = Encoding.GetEncoding("windows-1251").GetBytes(xml);

        var title = SenderTitleReader.Read(bytes);

        Assert.True(title.IsSuccess);
        Assert.Equal("ООО Ромашка", title.Value.Consignor);
        Assert.Equal("Ящики", title.Value.Lines[0].Name);
        Assert.Contains("ООО Ромашка", title.Value.RawXml);
    }

    private static byte[] Sample(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "XmlSamples", name));
}

public class ReceiverTitleWriterTests
{
    private static readonly EtrnIncomingDocumentEntity Document = new()
    {
        Number = "ТН-77",
        Date = "2026-11-04",
        Uid = "uid-1",
        Status = EtrnDocumentStatus.Sending,
        Lines =
        [
            new EtrnCargoLine { Index = 1, Places = 10, GrossWeight = "100" },
            new EtrnCargoLine { Index = 2, Places = 5, GrossWeight = "50" }
        ]
    };

    [Fact]
    public void Полный_приём_пишет_только_действие()
    {
        var xml = ReceiverTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Received,
            Status = EtrnDocumentStatus.Accepted
        });

        Assert.Contains("КНД=\"1110341\"", xml);
        Assert.Contains("Действие=\"Получен\"", xml);
        Assert.Contains("НомДок=\"ТН-77\"", xml);
        Assert.Contains("ДатаДок=\"04.11.2026\"", xml);
        Assert.DoesNotContain("КолМест", xml);
    }

    [Fact]
    public void Частичная_приёмка_пишет_факты_и_комментарии_строк()
    {
        var xml = ReceiverTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Partially,
            Status = EtrnDocumentStatus.Accepted,
            Lines =
            [
                new EtrnDecidedLine { Index = 1, FactPlaces = 9, FactWeight = "99.5", Comment = "недовоз" },
                new EtrnDecidedLine { Index = 2, FactPlaces = 5, FactWeight = "50", Comment = "общий" }
            ]
        });

        Assert.Contains("Действие=\"Получен частично\"", xml);
        Assert.Contains("НомСтр=\"1\"", xml);
        Assert.Contains("КолМест=\"9\"", xml);
        Assert.Contains("ВесБрутто=\"99.5\"", xml);
        Assert.Contains("Комментарий=\"недовоз\"", xml);
        Assert.Contains("Комментарий=\"общий\"", xml);
    }

    [Fact]
    public void Отказ_пишет_общий_комментарий_без_строк()
    {
        var xml = ReceiverTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Rejected,
            Status = EtrnDocumentStatus.Rejected,
            GeneralComment = "повреждён груз"
        });

        Assert.Contains("Действие=\"Не принят\"", xml);
        Assert.Contains("<Комментарий>повреждён груз</Комментарий>", xml);
        Assert.DoesNotContain("<СвГП", xml);
    }

    [Theory]
    [InlineData("ИдФайл")]
    [InlineData("ИмяФайла")]
    [InlineData("УИОЭДО")]
    [InlineData("Подписант")]
    [InlineData("ИдентификаторУчастника")]
    public void В_титуле_нет_имени_файла_участников_и_подписанта(string forbidden)
    {
        var xml = ReceiverTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Partially,
            Lines = [new EtrnDecidedLine { Index = 1, FactPlaces = 9, Comment = "недовоз" }]
        });

        Assert.DoesNotContain(forbidden, xml);
    }
}
