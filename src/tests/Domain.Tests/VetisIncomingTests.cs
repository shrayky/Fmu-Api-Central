using Domain.Vetis;

namespace Domain.Tests;

public class VetisIncomingStatusTests
{
    [Theory]
    [InlineData(new[] { "UTILIZED", "UTILIZED" }, VetisIncomingStatus.Accepted)]
    [InlineData(new[] { "UTILIZED", "CONFIRMED" }, VetisIncomingStatus.Partial)]
    [InlineData(new[] { "CONFIRMED", "CONFIRMED" }, VetisIncomingStatus.NotAccepted)]
    [InlineData(new[] { "WITHDRAWN", "WITHDRAWN" }, VetisIncomingStatus.Rejected)]
    [InlineData(new[] { "WITHDRAWN", "UTILIZED" }, VetisIncomingStatus.Accepted)]
    [InlineData(new[] { "WITHDRAWN", "CONFIRMED" }, VetisIncomingStatus.NotAccepted)]
    [InlineData(new string[0], VetisIncomingStatus.NotAccepted)]
    public void Of_считает_статус_по_ВСД(string[] vsdStatuses, string expected)
    {
        Assert.Equal(expected, VetisIncomingStatus.Of(vsdStatuses));
    }

    [Fact]
    public void Of_не_различает_регистр_статуса_ВСД()
    {
        Assert.Equal(VetisIncomingStatus.Accepted, VetisIncomingStatus.Of(["utilized"]));
    }
}

public class VetisIncomingDocumentTests
{
    [Fact]
    public void Group_собирает_ВСД_одной_накладной_в_один_документ()
    {
        var documents = VetisIncomingDocumentEntity.Group("o1", "e1",
        [
            Vsd("v1", "CONFIRMED", number: "ТТН-1", date: "2026-10-01"),
            Vsd("v2", "UTILIZED", number: "ТТН-1", date: "2026-10-01"),
            Vsd("v3", "CONFIRMED", number: "ТТН-2", date: "2026-10-01")
        ]);

        Assert.Equal(2, documents.Count);
        var first = documents.Single(item => item.Number == "ТТН-1");
        Assert.Equal(["v1", "v2"], first.Vsds.Select(item => item.Uuid));
        Assert.Equal("o1", first.OrganizationId);
        Assert.Equal("e1", first.EnterpriseGuid);
        Assert.Equal("2026-10-01", first.Date);
        Assert.False(first.Loose);
    }

    [Fact]
    public void Group_ВСД_без_накладной_группирует_по_дате_оформления_и_отправителю()
    {
        var documents = VetisIncomingDocumentEntity.Group("o1", "e1",
        [
            Vsd("v1", "CONFIRMED", issueDate: "2026-10-02T09:00:00+07:00", consignor: "Поставщик А"),
            Vsd("v2", "CONFIRMED", issueDate: "2026-10-02T17:30:00+07:00", consignor: "Поставщик А"),
            Vsd("v3", "CONFIRMED", issueDate: "2026-10-02T10:00:00+07:00", consignor: "Поставщик Б")
        ]);

        Assert.Equal(2, documents.Count);
        var group = documents.Single(item => item.Consignor == "Поставщик А");
        Assert.True(group.Loose);
        Assert.Equal("", group.Number);
        Assert.Equal("2026-10-02", group.Date);
        Assert.Equal(2, group.Vsds.Count);
    }

    [Fact]
    public void Group_даёт_один_и_тот_же_id_для_той_же_накладной()
    {
        var first = VetisIncomingDocumentEntity.Group("o1", "e1", [Vsd("v1", "CONFIRMED", number: "ТТН-1", date: "2026-10-01")]);
        var second = VetisIncomingDocumentEntity.Group("o1", "e1", [Vsd("v9", "UTILIZED", number: "ТТН-1", date: "2026-10-01")]);

        Assert.Equal(first.Single().Id, second.Single().Id);
        Assert.False(string.IsNullOrWhiteSpace(first.Single().Id));
    }

    [Fact]
    public void Group_разводит_одинаковую_накладную_по_площадкам_и_организациям()
    {
        var vsd = Vsd("v1", "CONFIRMED", number: "ТТН-1", date: "2026-10-01");

        var one = VetisIncomingDocumentEntity.Group("o1", "e1", [vsd]).Single().Id;
        var otherEnterprise = VetisIncomingDocumentEntity.Group("o1", "e2", [vsd]).Single().Id;
        var otherOrganization = VetisIncomingDocumentEntity.Group("o2", "e1", [vsd]).Single().Id;

        Assert.NotEqual(one, otherEnterprise);
        Assert.NotEqual(one, otherOrganization);
    }

    [Fact]
    public void Group_считает_статус_и_момент_сортировки()
    {
        var document = VetisIncomingDocumentEntity.Group("o1", "e1",
        [
            Vsd("v1", "UTILIZED", number: "ТТН-1", date: "2026-10-01", receivedAt: "2026-10-03T10:00:00+00:00"),
            Vsd("v2", "CONFIRMED", number: "ТТН-1", date: "2026-10-01", receivedAt: "2026-10-05T10:00:00+00:00")
        ]).Single();

        Assert.Equal(VetisIncomingStatus.Partial, document.Status);
        Assert.Equal("2026-10-05T10:00:00+00:00", document.ReceivedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-05T10:00:00+00:00").ToUnixTimeSeconds(), document.SortAt);
    }

    [Fact]
    public void Group_и_Merge_считают_ВСД_и_принятые_из_них()
    {
        var document = VetisIncomingDocumentEntity.Group("o1", "e1",
        [
            Vsd("v1", "UTILIZED", number: "ТТН-1", date: "2026-10-01"),
            Vsd("v2", "CONFIRMED", number: "ТТН-1", date: "2026-10-01")
        ]).Single();

        Assert.Equal(2, document.VsdCount);
        Assert.Equal(1, document.AcceptedCount);

        document.Merge([Vsd("v2", "UTILIZED", number: "ТТН-1", date: "2026-10-01")]);

        Assert.Equal(2, document.VsdCount);
        Assert.Equal(2, document.AcceptedCount);
    }

    [Fact]
    public void Merge_заменяет_ВСД_с_тем_же_uuid_и_пересчитывает_статус()
    {
        var document = VetisIncomingDocumentEntity.Group("o1", "e1",
        [
            Vsd("v1", "CONFIRMED", number: "ТТН-1", date: "2026-10-01"),
            Vsd("v2", "CONFIRMED", number: "ТТН-1", date: "2026-10-01")
        ]).Single();

        document.Merge([Vsd("v1", "UTILIZED", number: "ТТН-1", date: "2026-10-01"), Vsd("v3", "UTILIZED", number: "ТТН-1", date: "2026-10-01")]);

        Assert.Equal(["v1", "v2", "v3"], document.Vsds.Select(item => item.Uuid).OrderBy(item => item));
        Assert.Equal("UTILIZED", document.Vsds.Single(item => item.Uuid == "v1").Status);
        Assert.Equal(VetisIncomingStatus.Partial, document.Status);
    }

    [Fact]
    public void SortAt_без_гашения_не_подставляет_дату_обновления_и_оформления()
    {
        var updated = VetisIncomingDocumentEntity.Group("o1", "e1",
            [Vsd("v1", "CONFIRMED", number: "ТТН-1", date: "2026-10-01", updatedAt: "2026-10-04T08:00:00+00:00")]).Single();
        var issued = VetisIncomingDocumentEntity.Group("o1", "e1",
            [Vsd("v2", "CONFIRMED", number: "ТТН-2", date: "2026-10-01", issueDate: "2026-10-02T08:00:00+00:00")]).Single();

        Assert.Equal("", updated.ReceivedAt);
        Assert.Equal(0, updated.SortAt);
        Assert.Equal("", issued.ReceivedAt);
        Assert.Equal(0, issued.SortAt);
    }

    private static VetisIncomingVsd Vsd(
        string uuid,
        string status,
        string number = "",
        string date = "",
        string issueDate = "2026-10-01T08:00:00+00:00",
        string consignor = "Поставщик",
        string receivedAt = "",
        string updatedAt = "") =>
        new()
        {
            Uuid = uuid,
            Status = status,
            SourceNumber = number,
            SourceDate = date,
            SourceType = number.Length == 0 ? "" : "1",
            IssueDate = issueDate,
            ConsignorName = consignor,
            ReceivedAt = receivedAt,
            UpdatedAt = updatedAt
        };
}

public class ReferencedIncomingDocumentTests
{
    [Fact]
    public void Read_берёт_транспортную_накладную_и_пропускает_прочие_типы()
    {
        var xml = """
            <vetDocument>
              <referencedDocument><type>9</type><issueNumber>ПРОЧЕЕ</issueNumber><issueDate>2026-09-01</issueDate></referencedDocument>
              <referencedDocument><type>2</type><issueNumber>ТН-7</issueNumber><issueDate>2026-10-01</issueDate><relationshipType>2</relationshipType></referencedDocument>
              <referencedDocument><type>1</type><issueNumber>ТТН-5</issueNumber><issueDate>2026-10-02</issueDate><relationshipType>1</relationshipType></referencedDocument>
            </vetDocument>
            """;

        var source = ReferencedIncomingDocument.Read(xml);

        Assert.Equal("ТТН-5", source.Number);
        Assert.Equal("2026-10-02", source.Date);
        Assert.Equal("1", source.Type);
    }

    [Fact]
    public void Read_без_накладной_и_с_битым_xml_возвращает_пустое()
    {
        Assert.Equal("", ReferencedIncomingDocument.Read("<vetDocument/>").Number);
        Assert.Equal("", ReferencedIncomingDocument.Read("не xml").Number);
        Assert.Equal("", ReferencedIncomingDocument.Read(null).Number);
    }
}

public class IncomingChangesWindowTests
{
    private static readonly DateTimeOffset End = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Begin_без_дат_отступает_на_срок_среза()
    {
        Assert.Equal(End - TimeSpan.FromDays(IncomingChangesWindow.SliceDays), IncomingChangesWindow.Begin(End));
    }

    [Fact]
    public void Begin_предпочитает_дату_площадки_затем_организации()
    {
        var own = IncomingChangesWindow.Begin(End, "2026-10-01", "2026-10-10");
        var shared = IncomingChangesWindow.Begin(End, "2026-10-01", "");

        Assert.Equal(IncomingChangesWindow.StartOf(new DateOnly(2026, 10, 10)), own);
        Assert.Equal(IncomingChangesWindow.StartOf(new DateOnly(2026, 10, 1)), shared);
    }

    [Fact]
    public void Begin_принимает_момент_курсора_целиком_и_сжимает_будущее_к_концу()
    {
        var cursor = IncomingChangesWindow.Begin(End, null, "2026-10-20T08:30:00+00:00");
        var future = IncomingChangesWindow.Begin(End, null, "2027-01-01");

        Assert.Equal(new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.Zero), cursor);
        Assert.Equal(End, future);
    }

    [Fact]
    public void Slices_режет_период_на_куски_не_длиннее_среза()
    {
        var begin = End - TimeSpan.FromDays(75);

        var slices = IncomingChangesWindow.Slices(begin, End);

        Assert.Equal(3, slices.Count);
        Assert.Equal(begin, slices[0].Begin);
        Assert.Equal(End, slices[^1].End);
        Assert.All(slices, slice => Assert.True(slice.End - slice.Begin <= TimeSpan.FromDays(IncomingChangesWindow.SliceDays)));
        Assert.Equal(slices[0].End, slices[1].Begin);
    }

    [Fact]
    public void NextFrom_берёт_последнее_обновление_иначе_конец_покрытого_окна()
    {
        var update = End.AddDays(-1);

        Assert.Equal(update, IncomingChangesWindow.NextFrom(update, End));
        Assert.Equal(End, IncomingChangesWindow.NextFrom(null, End));
        Assert.Null(IncomingChangesWindow.NextFrom(null, null));
    }

    [Fact]
    public void RejectsInterval_узнаёт_отказ_по_длине_окна_только_для_длинного_окна()
    {
        Assert.True(IncomingChangesWindow.RejectsInterval("Недопустимый интервал дат", End.AddDays(-5), End));
        Assert.False(IncomingChangesWindow.RejectsInterval("Недопустимый интервал дат", End.AddMinutes(-30), End));
        Assert.False(IncomingChangesWindow.RejectsInterval("Нет доступа", End.AddDays(-5), End));
    }

    [Fact]
    public void Midpoint_делит_окно_пополам()
    {
        Assert.Equal(End.AddDays(-5), IncomingChangesWindow.Midpoint(End.AddDays(-10), End));
    }
}

public class VetisDateTimeParseTests
{
    [Fact]
    public void TryParse_читает_дату_с_поясом_и_отклоняет_мусор()
    {
        Assert.True(VetisDateTime.TryParse("2026-10-20T08:30:00+07:00", out var instant));
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.FromHours(7)), instant);
        Assert.False(VetisDateTime.TryParse("вчера", out _));
        Assert.False(VetisDateTime.TryParse("", out _));
    }
}

public class VetisTransportTests
{
    [Fact]
    public void Describe_собирает_тип_и_номер_без_способа_хранения()
    {
        var xml = """
            <vd:vetDocument xmlns:vd="urn:vet">
              <vd:certifiedConsignment>
                <vd:transportInfo>
                  <vd:transportType>1</vd:transportType>
                  <vd:transportNumber><vd:vehicleNumber>0724KP124</vd:vehicleNumber></vd:transportNumber>
                </vd:transportInfo>
                <vd:transportStorageType>CHILLED</vd:transportStorageType>
              </vd:certifiedConsignment>
            </vd:vetDocument>
            """;

        Assert.Equal("Автомобиль 0724KP124", VetisTransport.Describe(xml));
    }

    [Theory]
    [InlineData("2", "wagonNumber", "512", "Железнодорожный 512")]
    [InlineData("3", "flightNumber", "SU100", "Авиа SU100")]
    [InlineData("4", "containerNumber", "MSKU1", "Морской контейнер MSKU1")]
    [InlineData("5", "shipName", "Волга", "Морской трюм Волга")]
    [InlineData("6", "shipName", "Енисей", "Речной Енисей")]
    [InlineData("7", "vehicleNumber", "1", "Перегон 1")]
    public void Describe_называет_тип_транспорта_по_коду(string type, string numberField, string number, string expected)
    {
        var xml = $"""
            <root>
              <transportInfo>
                <transportType>{type}</transportType>
                <transportNumber><{numberField}>{number}</{numberField}></transportNumber>
              </transportInfo>
            </root>
            """;

        Assert.Equal(expected, VetisTransport.Describe(xml));
    }

    [Fact]
    public void Describe_показывает_машину_и_прицеп()
    {
        var xml = """
            <root>
              <transportInfo>
                <transportType>1</transportType>
                <transportNumber>
                  <vehicleNumber>А111АА</vehicleNumber>
                  <trailerNumber>В222ВВ</trailerNumber>
                </transportNumber>
              </transportInfo>
            </root>
            """;

        Assert.Equal("Автомобиль А111АА, В222ВВ", VetisTransport.Describe(xml));
    }

    [Fact]
    public void Describe_оставляет_неизвестный_код_как_есть()
    {
        var xml = "<root><transportInfo><transportType>9</transportType></transportInfo><transportStorageType>RAW</transportStorageType></root>";

        Assert.Equal("9", VetisTransport.Describe(xml));
        Assert.Equal("", VetisTransport.Describe("<root><transportStorageType>CHILLED</transportStorageType></root>"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("не xml")]
    [InlineData("<vetDocument/>")]
    public void Describe_без_транспорта_пустой(string? xml)
    {
        Assert.Equal("", VetisTransport.Describe(xml));
    }
}

public class VetisXmlFormatTests
{
    [Fact]
    public void Pretty_расставляет_отступы_вложенным_элементам()
    {
        var text = VetisXmlFormat.Pretty("<a><b>1</b><c/></a>");

        Assert.Contains("\n", text);
        Assert.Contains("  <b>1</b>", text);
    }

    [Fact]
    public void Pretty_возвращает_не_xml_и_пустое_как_есть()
    {
        Assert.Equal("не xml", VetisXmlFormat.Pretty("не xml"));
        Assert.Equal("", VetisXmlFormat.Pretty(""));
        Assert.Equal("", VetisXmlFormat.Pretty(null));
    }
}