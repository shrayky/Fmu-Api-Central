using System.Xml.Linq;
using Domain.Vetis;
using VetisApi.Xml;

namespace Application.Tests;

public class VetisQuenchXmlTests
{
    [Fact]
    public void Полная_партия_принимается_без_акта()
    {
        var order = VetisQuench.Decide("10", "10").Value;
        var built = ProcessIncomingConsignmentXml.Create(Call(), SampleVsd, order, new DateOnly(2026, 10, 30));

        Assert.True(built.IsSuccess, built.IsFailure ? built.Error : "");
        var xml = built.Value.ToString();
        Assert.Contains("processIncomingConsignmentRequest", xml);
        Assert.Contains("ACCEPT_ALL", xml);
        Assert.Contains("ELECTRONIC", xml);
        Assert.Contains("11111111-1111-1111-1111-111111111111", xml);
        Assert.Contains("А000АА77", xml);
        Assert.Contains("ТТН-9", xml);
        Assert.DoesNotContain("ТН-7", xml);
        Assert.DoesNotContain("discrepancyReport", xml);
        Assert.DoesNotContain("shipmentRoute", xml);
        Assert.DoesNotContain("Молоко", xml);
    }

    [Fact]
    public void Собственник_и_классификации_не_уходят_в_заявку()
    {
        var vsd = SampleVsd.Replace(
            "<vd:lowGradeCargo>false</vd:lowGradeCargo>",
            """
            <vd:lowGradeCargo>false</vd:lowGradeCargo>
            <vd:owner><bs:guid>abababab-2222-2222-2222-abababababab</bs:guid></vd:owner>
            <vd:applicableClassifications>
              <vd:attribute><dt:attrCode>MILK_FAT_MASS_FRACTION</dt:attrCode></vd:attribute>
            </vd:applicableClassifications>
            """);
        var order = VetisQuench.Decide("10", "10").Value;

        var built = ProcessIncomingConsignmentXml.Create(Call(), vsd, order, new DateOnly(2026, 10, 30));

        Assert.True(built.IsSuccess, built.IsFailure ? built.Error : "");
        var xml = built.Value.ToString();
        Assert.DoesNotContain("owner", xml);
        Assert.DoesNotContain("applicableClassifications", xml);
        Assert.DoesNotContain("abababab-2222-2222-2222-abababababab", xml);
    }

    [Fact]
    public void Ссылки_партии_уходят_одним_guid()
    {
        var vsd = SampleVsd.Replace(
            "<vd:product><bs:guid>eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee</bs:guid></vd:product>",
            "<vd:product><bs:uuid>11111111-2222-2222-2222-111111111111</bs:uuid><bs:guid>eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee</bs:guid><dt:name>Молоко и молочная продукция</dt:name></vd:product>");
        var order = VetisQuench.Decide("10", "10").Value;

        var built = ProcessIncomingConsignmentXml.Create(Call(), vsd, order, new DateOnly(2026, 10, 30));

        Assert.True(built.IsSuccess, built.IsFailure ? built.Error : "");
        var product = XElement.Parse(built.Value.ToString())
            .Descendants()
            .Single(element => element.Name.LocalName == "product");
        Assert.Equal("guid", product.Elements().Single().Name.LocalName);
        Assert.Equal("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", product.Elements().Single().Value);
    }

    [Fact]
    public void Происхождение_без_номенклатуры()
    {
        var vsd = SampleVsd.Replace(
            "<vd:origin><vd:country>",
            "<vd:origin><vd:productItem><bs:guid>34343434-3434-3434-3434-343434343434</bs:guid></vd:productItem><vd:country>");
        var order = VetisQuench.Decide("10", "10").Value;

        var built = ProcessIncomingConsignmentXml.Create(Call(), vsd, order, new DateOnly(2026, 10, 30));

        Assert.True(built.IsSuccess, built.IsFailure ? built.Error : "");
        var origin = XElement.Parse(built.Value.ToString())
            .Descendants()
            .Single(element => element.Name.LocalName == "origin");
        Assert.DoesNotContain(origin.Elements(), element => element.Name.LocalName == "productItem");
        Assert.DoesNotContain("34343434-3434-3434-3434-343434343434", built.Value.ToString());
    }

    /// <summary>
    /// Принятая заявка гашения идёт схемой applications/v2: сервис 3.0 с тем же составом партии отвечает MERC14261.
    /// </summary>
    [Fact]
    public void Заявка_гашения_в_пространстве_applications_v2()
    {
        var order = VetisQuench.Decide("10", "10").Value;
        var built = ProcessIncomingConsignmentXml.Create(Call(), SampleVsd, order, new DateOnly(2026, 10, 30));

        Assert.True(built.IsSuccess, built.IsFailure ? built.Error : "");
        Assert.Equal("http://api.vetrf.ru/schema/cdm/mercury/g2b/applications/v2", built.Value.Name.NamespaceName);
        Assert.Equal(
            "http://api.vetrf.ru/schema/cdm/mercury/g2b/applications/v2",
            built.Value.Elements().Single(element => element.Name.LocalName == "localTransactionId").Name.NamespaceName);
    }

    /// <summary>
    /// Схема 2.1 принимает год, месяц и день в dictionary. На 3.0 такие даты отклоняются, поэтому гашение туда не отправляется.
    /// </summary>
    [Fact]
    public void Даты_партии_пишутся_в_пространстве_имён_dictionary()
    {
        var vsd = SampleVsd.Replace(
            "<bs:year>2026</bs:year><bs:month>10</bs:month><bs:day>1</bs:day>",
            "<dt:year>2026</dt:year><dt:month>10</dt:month><dt:day>1</dt:day>");
        var order = VetisQuench.Decide("10", "10").Value;
        var built = ProcessIncomingConsignmentXml.Create(Call(), vsd, order, new DateOnly(2026, 10, 30));

        Assert.True(built.IsSuccess, built.IsFailure ? built.Error : "");
        var year = XElement.Parse(built.Value.ToString())
            .Descendants()
            .Single(element => element.Name.LocalName == "year");
        Assert.Equal("http://api.vetrf.ru/schema/cdm/dictionary/v2", year.Name.NamespaceName);
        Assert.Equal("2026", year.Value);
    }

    [Fact]
    public void Упаковка_сохраняет_код_и_название()
    {
        var vsd = SampleVsd.Replace(
            "<vd:lowGradeCargo>false</vd:lowGradeCargo>",
            """
            <vd:lowGradeCargo>false</vd:lowGradeCargo>
            <vd:packageList>
              <dt:package>
                <dt:level>2</dt:level>
                <dt:packingType>
                  <bs:uuid>483400f6-eef8-4d94-bebe-9a6bbc5585e8</bs:uuid>
                  <bs:guid>fedf4328-053c-11e1-99b4-d8d385fbc9e8</bs:guid>
                  <dt:globalID>CT</dt:globalID>
                  <dt:name>Коробка, картонная</dt:name>
                </dt:packingType>
                <dt:quantity>1</dt:quantity>
              </dt:package>
            </vd:packageList>
            """);
        var order = VetisQuench.Decide("10", "10").Value;

        var built = ProcessIncomingConsignmentXml.Create(Call(), vsd, order, new DateOnly(2026, 10, 30));

        Assert.True(built.IsSuccess, built.IsFailure ? built.Error : "");
        var packing = XElement.Parse(built.Value.ToString())
            .Descendants()
            .Single(element => element.Name.LocalName == "packingType");
        Assert.Equal(
            ["uuid", "guid", "globalID", "name"],
            packing.Elements().Select(element => element.Name.LocalName).ToArray());
        Assert.Contains("Коробка, картонная", packing.ToString());
    }

    [Fact]
    public void Стороны_уходят_по_guid_без_версии_uuid()
    {
        var vsd = SampleVsd.Replace(
            "<dt:businessEntity><bs:guid>aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa</bs:guid>",
            "<dt:businessEntity><bs:uuid>dddddddd-1111-1111-1111-dddddddddddd</bs:uuid><bs:guid>aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa</bs:guid>");
        var order = VetisQuench.Decide("10", "10").Value;

        var built = ProcessIncomingConsignmentXml.Create(Call(), vsd, order, new DateOnly(2026, 10, 30));

        Assert.True(built.IsSuccess, built.IsFailure ? built.Error : "");
        var xml = built.Value.ToString();
        Assert.Contains("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", xml);
        Assert.DoesNotContain("dddddddd-1111-1111-1111-dddddddddddd", xml);
    }

    [Fact]
    public void Перевозчик_уходит_одним_guid()
    {
        var vsd = SampleVsd.Replace(
            "<vd:transportInfo>",
            """
            <vd:broker>
              <bs:uuid>9956cbfb-b65b-4ba0-b0d6-0584789bd125</bs:uuid>
              <bs:guid>532c7495-f361-4028-9b40-666b339abb1e</bs:guid>
              <dt:fio>ИЗВЕКОВ</dt:fio>
            </vd:broker>
            <vd:transportInfo>
            """);
        var order = VetisQuench.Decide("10", "10").Value;

        var built = ProcessIncomingConsignmentXml.Create(Call(), vsd, order, new DateOnly(2026, 10, 30));

        Assert.True(built.IsSuccess, built.IsFailure ? built.Error : "");
        var broker = XElement.Parse(built.Value.ToString())
            .Descendants()
            .Single(element => element.Name.LocalName == "broker");
        Assert.Equal("guid", broker.Elements().Single().Name.LocalName);
        Assert.Equal("532c7495-f361-4028-9b40-666b339abb1e", broker.Elements().Single().Value);
    }

    [Fact]
    public void Разница_подставляет_факт_и_акт_расхождения()
    {
        var order = VetisQuench.Decide("10", "4").Value;
        var built = ProcessIncomingConsignmentXml.Create(Call(), SampleVsd, order, new DateOnly(2026, 10, 30));

        var xml = built.Value.ToString();
        Assert.Contains("PARTIALLY", xml);
        Assert.Equal("4", Volume(xml));
        Assert.Contains("discrepancyReport", xml);
        Assert.Contains("2026-10-30", xml);
        Assert.Contains("Принято 4 из 10", xml);
    }

    [Fact]
    public void Ноль_возвращает_партию_и_оставляет_объём_поступления()
    {
        var order = VetisQuench.Decide("10", "0").Value;
        var xml = ProcessIncomingConsignmentXml.Create(Call(), SampleVsd, order, new DateOnly(2026, 10, 30)).Value.ToString();

        Assert.Contains("RETURN_ALL", xml);
        Assert.Equal("10", Volume(xml));
        Assert.Contains("discrepancyReport", xml);
        Assert.DoesNotContain(">0<", xml);
    }

    [Fact]
    public void Акт_пишет_оба_объёма_с_точкой()
    {
        var order = VetisQuench.Decide("10,5", "4,5").Value;
        var xml = ProcessIncomingConsignmentXml.Create(Call(), SampleVsd, order, new DateOnly(2026, 10, 30)).Value.ToString();

        Assert.Contains("Принято 4.5 из 10.5", xml);
        Assert.DoesNotContain("10,5", xml);
    }

    [Fact]
    public void Дата_доставки_идёт_со_смещением()
    {
        var order = VetisQuench.Decide("10", "4").Value;
        var xml = ProcessIncomingConsignmentXml.Create(Call(), SampleVsd, order, new DateOnly(2026, 10, 30)).Value.ToString();

        var delivery = XElement.Parse(xml).Descendants().Single(element => element.Name.LocalName == "deliveryDate").Value;

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$", delivery);
        Assert.Contains("2026-10-30", xml);
    }

    [Fact]
    public void Ответ_гашения_берёт_дату_перехода_в_UTILIZED()
    {
        const string xml = """
            <root>
              <vetDocument>
                <uuid>v-1</uuid>
                <vetDStatus>UTILIZED</vetDStatus>
                <statusChange><status>CONFIRMED</status><actualDateTime>2026-10-02T12:00:00+07:00</actualDateTime></statusChange>
                <statusChange><status>UTILIZED</status><actualDateTime>2026-10-30T08:00:00+07:00</actualDateTime></statusChange>
              </vetDocument>
            </root>
            """;

        var row = VetisResponseReader.ReadQuenched(xml, "v-1");

        Assert.NotNull(row);
        Assert.Equal("UTILIZED", row.Status);
        Assert.Equal("2026-10-30T08:00:00+07:00", row.ReceivedAt);
    }

    [Fact]
    public void Ответ_гашения_не_берёт_пустую_ссылку_из_складской_записи()
    {
        const string xml = """
            <receiveApplicationResultResponse>
              <application>
                <status>COMPLETED</status>
                <result>
                  <processIncomingConsignmentResponse>
                    <stockEntry>
                      <status>102</status>
                      <vetDocument>
                        <uuid>925547ae-ecd4-43bd-b893-28143cda7719</uuid>
                      </vetDocument>
                    </stockEntry>
                    <vetDocument>
                      <uuid>925547ae-ecd4-43bd-b893-28143cda7719</uuid>
                      <vetDStatus>UTILIZED</vetDStatus>
                      <statusChange>
                        <status>CONFIRMED</status>
                        <actualDateTime>2026-02-22T16:23:38+03:00</actualDateTime>
                      </statusChange>
                      <statusChange>
                        <status>UTILIZED</status>
                        <actualDateTime>2026-10-30T11:14:21+03:00</actualDateTime>
                      </statusChange>
                    </vetDocument>
                  </processIncomingConsignmentResponse>
                </result>
              </application>
            </receiveApplicationResultResponse>
            """;

        Assert.Equal("COMPLETED", VetisResponseReader.ApplicationStatus(xml));
        Assert.Null(VetisResponseReader.ErrorText(xml));
        var row = VetisResponseReader.ReadQuenched(xml, "925547ae-ecd4-43bd-b893-28143cda7719");

        Assert.NotNull(row);
        Assert.Equal("UTILIZED", row.Status);
        Assert.Equal("2026-10-30T11:14:21+03:00", row.ReceivedAt);
    }

    private static string Volume(string xml) =>
        XElement.Parse(xml).Descendants().Single(element => element.Name.LocalName == "volume").Value;

    private static VetisCall Call() => new()
    {
        InitiatorLogin = "operator-1",
        IssuerId = "issuer-1",
        ApiKey = "key-1"
    };

    private const string SampleVsd =
        """
        <vd:vetDocument xmlns:vd="http://api.vetrf.ru/schema/cdm/mercury/vet-document/v2" xmlns:bs="http://api.vetrf.ru/schema/cdm/base" xmlns:dt="http://api.vetrf.ru/schema/cdm/dictionary/v2">
          <bs:uuid>11111111-1111-1111-1111-111111111111</bs:uuid>
          <vd:certifiedConsignment>
            <vd:consignor>
              <dt:businessEntity><bs:guid>aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa</bs:guid><dt:fullName>Отправитель</dt:fullName></dt:businessEntity>
              <dt:enterprise><bs:guid>bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb</bs:guid></dt:enterprise>
            </vd:consignor>
            <vd:consignee>
              <dt:businessEntity><bs:guid>cccccccc-cccc-cccc-cccc-cccccccccccc</bs:guid></dt:businessEntity>
              <dt:enterprise><bs:guid>dddddddd-dddd-dddd-dddd-dddddddddddd</bs:guid><dt:name>Склад</dt:name></dt:enterprise>
            </vd:consignee>
            <vd:transportInfo>
              <vd:transportType>1</vd:transportType>
              <vd:transportNumber><vd:vehicleNumber>А000АА77</vd:vehicleNumber></vd:transportNumber>
            </vd:transportInfo>
            <vd:batch>
              <vd:productType>1</vd:productType>
              <vd:product><bs:guid>eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee</bs:guid></vd:product>
              <vd:subProduct><bs:guid>ffffffff-ffff-ffff-ffff-ffffffffffff</bs:guid></vd:subProduct>
              <vd:productItem><bs:guid>99999999-9999-9999-9999-999999999999</bs:guid><dt:name>Молоко</dt:name></vd:productItem>
              <vd:volume>10</vd:volume>
              <vd:unit><bs:guid>12121212-1212-1212-1212-121212121212</bs:guid></vd:unit>
              <vd:dateOfProduction><vd:firstDate><bs:year>2026</bs:year><bs:month>10</bs:month><bs:day>1</bs:day></vd:firstDate></vd:dateOfProduction>
              <vd:perishable>true</vd:perishable>
              <vd:origin><vd:country><bs:guid>87878787-8787-8787-8787-878787878787</bs:guid></vd:country></vd:origin>
              <vd:lowGradeCargo>false</vd:lowGradeCargo>
            </vd:batch>
            <vd:shipmentRoute><vd:routePoint><bs:uuid>abababab-abab-abab-abab-abababababab</bs:uuid></vd:routePoint></vd:shipmentRoute>
          </vd:certifiedConsignment>
          <vd:referencedDocument>
            <vd:issueNumber>ТН-7</vd:issueNumber>
            <vd:issueDate>2026-10-01</vd:issueDate>
            <vd:type>2</vd:type>
            <vd:relationshipType>2</vd:relationshipType>
          </vd:referencedDocument>
          <vd:referencedDocument>
            <vd:issueNumber>ТТН-9</vd:issueNumber>
            <vd:issueDate>2026-10-01</vd:issueDate>
            <vd:type>1</vd:type>
            <vd:relationshipType>1</vd:relationshipType>
          </vd:referencedDocument>
        </vd:vetDocument>
        """;
}
