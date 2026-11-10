using System.Text;
using Domain.Etrn;
using Domain.Etrn.Xml;
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
    public void Номер_накладной_и_ФИО_не_берутся_из_сопроводительного_документа()
    {
        var title = SenderTitleReader.Read(Sample("sender-waybill-and-attachment.xml"));

        Assert.True(title.IsSuccess, title.IsFailure ? title.Error : "");
        Assert.Equal("ТН-99", title.Value.Number);
        Assert.Equal("2026-11-09", title.Value.Date);
        Assert.Equal("uid-waybill", title.Value.Uid);
        Assert.Equal("Козлов Евгений Владимирович", title.Value.Consignor);
    }

    [Fact]
    public void Адрес_доставки_читается_из_текста_и_из_полей()
    {
        var text = SenderTitleReader.Read(Encoding.UTF8.GetBytes("""
            <?xml version="1.0" encoding="utf-8"?>
            <Файл>
              <Документ КНД="1110339">
                <СодИнфГО НомерТрН="ТН-1" ДатаТрН="14.09.2026">
                  <СвГО>
                    <Адрес>
                      <АдресИнф АдрТекст="адрес грузоотправителя, не выгрузка"/>
                    </Адрес>
                  </СвГО>
                  <СвГП>
                    <АдресДостГр>
                      <АдресИнф КодСтр="643" АдрТекст="Красноярский край, г. Красноярск, пр-кт 60 лет образования СССР, д. 21/5"/>
                    </АдресДостГр>
                  </СвГП>
                </СодИнфГО>
              </Документ>
            </Файл>
            """));
        var parts = SenderTitleReader.Read(Encoding.UTF8.GetBytes("""
            <?xml version="1.0" encoding="utf-8"?>
            <Файл>
              <Документ КНД="1110339">
                <СодИнфГО НомерТрН="ТН-2" ДатаТрН="14.09.2026">
                  <СвГП>
                    <АдресДостГр>
                      <АдресРФ Индекс="660000" КодРегион="24" Город="Красноярск" Улица="Ленина" Дом="5" Кварт="2"/>
                    </АдресДостГр>
                  </СвГП>
                </СодИнфГО>
              </Документ>
            </Файл>
            """));

        Assert.True(text.IsSuccess, text.IsFailure ? text.Error : "");
        Assert.Equal("Красноярский край, г. Красноярск, пр-кт 60 лет образования СССР, д. 21/5", text.Value.Address);
        Assert.True(parts.IsSuccess, parts.IsFailure ? parts.Error : "");
        Assert.Equal("660000, 24, Красноярск, Ленина, 5, 2", parts.Value.Address);
    }

    [Fact]
    public void Плановая_масса_берётся_из_ПлМасГруз()
    {
        var title = SenderTitleReader.Read(Encoding.UTF8.GetBytes("""
            <?xml version="1.0" encoding="utf-8"?>
            <Файл ВерсФорм="5.01">
              <Документ КНД="1110339">
                <СодИнфГО НомерТрН="ТН-1" ДатаТрН="10.11.2026">
                  <СвГруз>
                    <ОпГруз НаимГруз="Сыр" КолМестГр="7">
                      <ПлМасГруз МасБрутЗнач="33.382"/>
                    </ОпГруз>
                  </СвГруз>
                </СодИнфГО>
              </Документ>
            </Файл>
            """));

        Assert.True(title.IsSuccess, title.IsFailure ? title.Error : "");
        var line = Assert.Single(title.Value.Lines);
        Assert.Equal(7, line.Places);
        Assert.Equal("33.382", line.GrossWeight);
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
    private static readonly DateTimeOffset SignedAt = new(2026, 11, 10, 13, 5, 0, TimeSpan.FromHours(7));

    private static readonly EtrnIncomingDocumentEntity Document = new()
    {
        Number = "ТН-77",
        Date = "2026-11-04",
        Uid = "uid-1",
        DeliveryAddress = "Красноярск, Ленина, 5",
        Status = EtrnDocumentStatus.Sending,
        Lines =
        [
            new EtrnCargoLine { Index = 1, Places = 10, GrossWeight = "100" },
            new EtrnCargoLine { Index = 2, Places = 5, GrossWeight = "50" }
        ]
    };

    [Fact]
    public void Полный_приём_пишет_прием_груза_суммой_титула()
    {
        var xml = ReceiverTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Received,
            Status = EtrnDocumentStatus.Accepted
        }, SignedAt);

        Assert.Contains("КНД=\"1110341\"", xml);
        Assert.Contains("УИД_ТрН=\"uid-1\"", xml);
        Assert.Contains("СодОпПр=\"Лицом, управомоченным на получение груза, при указанных обстоятельствах принят от перевозчика груз с указанными характеристиками\"", xml);
        Assert.Contains("ФДатВрПриб=\"10.11.2026T13:05:00+07:00\"", xml);
        Assert.Contains("ФДатВрУбыт=\"10.11.2026T13:05:00+07:00\"", xml);
        Assert.Contains("ЗаявДатВрПриб=\"10.11.2026T13:05:00+07:00\"", xml);
        Assert.Contains("НалКоорТочВрФПр=\"1\"", xml);
        Assert.Contains("МетОпрМасс=\"03\"", xml);
        Assert.Contains("КолМестПриемЧ=\"15\"", xml);
        Assert.Contains("МасБрутЗначПрием=\"150\"", xml);
        Assert.Contains("ОбщСвСост=\"груз принят\"", xml);
        Assert.Contains("АдрТекст=\"Красноярск, Ленина, 5\"", xml);
        Assert.Contains("<ПриемГрузГП", xml);
        Assert.DoesNotContain("СодОпОтк", xml);
    }

    [Fact]
    public void Частичная_приёмка_пишет_сумму_фактов_и_комментарии()
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
        }, SignedAt);

        Assert.Contains("КолМестПриемЧ=\"14\"", xml);
        Assert.Contains("МасБрутЗначПрием=\"149.5\"", xml);
        Assert.Contains("ОбщСвСост=\"недовоз; общий\"", xml);
        Assert.Contains("<ПриемГрузГП", xml);
    }

    [Fact]
    public void Отказ_пишет_причину_без_приема_груза()
    {
        var xml = ReceiverTitleWriter.Build(Document, new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Rejected,
            Status = EtrnDocumentStatus.Rejected,
            GeneralComment = "повреждён груз"
        }, SignedAt);

        Assert.Contains("СодОпОтк=\"Лицом, управомоченным на получение груза, при указанных обстоятельствах отказано в принятии от перевозчика груза с указанными характеристиками\"", xml);
        Assert.Contains("ПричОтк=\"повреждён груз\"", xml);
        Assert.DoesNotContain("ПриемГрузГП", xml);
        Assert.DoesNotContain("СодОпПр", xml);
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
        }, SignedAt);

        Assert.DoesNotContain(forbidden, xml);
    }
}
