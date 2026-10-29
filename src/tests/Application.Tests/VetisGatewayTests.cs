using System.Net;
using System.Text;
using Domain.Configuration;
using Domain.Vetis;
using VetisApi;
using VetisApi.Xml;

namespace Application.Tests;

public class VetisGatewayTests
{
    /// <summary>
    /// Guid и название берутся у enterprise, не у хозяйствующего субъекта.
    /// </summary>
    [Fact]
    public void ReadEnterprises_берёт_площадку()
    {
        var xml = """
            <root>
              <activityLocationList total="1">
                <activityLocation>
                  <businessEntity>
                    <guid>subject-guid</guid>
                    <name>Субъект</name>
                  </businessEntity>
                  <enterprise>
                    <guid>aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa</guid>
                    <name>Склад</name>
                    <address><addressView>ул. Ленина, 12</addressView></address>
                  </enterprise>
                </activityLocation>
              </activityLocationList>
            </root>
            """;

        var row = Assert.Single(VetisResponseReader.ReadEnterprises(xml));

        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", row.Guid);
        Assert.Equal("Склад", row.Name);
        Assert.Equal("ул. Ленина, 12", row.Address);
    }

    /// <summary>
    /// Проверка шлёт Basic и SOAPAction, пароль в журнал не нужен: в теле запроса его нет.
    /// </summary>
    [Fact]
    public async Task Check_отправляет_basic_и_не_кладёт_пароль_в_тело()
    {
        var handler = new CaptureHandler("""
            <root><applicationId>app-1</applicationId><status>COMPLETED</status></root>
            """);
        var gateway = new VetisGateway(new StubFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<VetisGateway>.Instance);

        var result = await gateway.CheckConnectionAsync(new VetisCall
        {
            Endpoint = "https://vetis.example/ApplicationManagementService",
            ApiKey = "key-1",
            BasicLogin = "user-1",
            BasicPassword = "secret-1",
            IssuerId = "issuer-1",
            InitiatorLogin = "operator-1",
            ServiceId = VetisEndpoints.ServiceId,
            ResultPollAttempts = 1
        }, CancellationToken.None);

        Assert.True(result.Completed);
        Assert.Equal("app-1", result.ApplicationId);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user-1:secret-1")), handler.Authorization);
        Assert.Equal("submitApplicationRequest", handler.SoapAction);
        Assert.DoesNotContain("secret-1", handler.Body);
        Assert.Contains("key-1", handler.Body);
        Assert.Contains("operator-1", handler.Body);
    }

    /// <summary>
    /// Опрос ACCEPTED не получает новый таймаут на каждую попытку: вся проверка укладывается в TimeoutSeconds.
    /// </summary>
    [Fact]
    public async Task Check_не_ждёт_дольше_суммарного_таймаута()
    {
        var handler = new CaptureHandler("""
            <root><applicationId>app-1</applicationId><status>ACCEPTED</status></root>
            """);
        var gateway = new VetisGateway(new StubFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<VetisGateway>.Instance);
        var started = System.Diagnostics.Stopwatch.StartNew();

        var result = await gateway.CheckConnectionAsync(new VetisCall
        {
            Endpoint = "https://vetis.example/ApplicationManagementService",
            ApiKey = "key-1",
            BasicLogin = "user-1",
            BasicPassword = "secret-1",
            IssuerId = "issuer-1",
            InitiatorLogin = "operator-1",
            TimeoutSeconds = 1,
            ResultPollAttempts = 8,
            ResultPollDelaySeconds = 30
        }, CancellationToken.None);

        started.Stop();
        Assert.False(result.Completed);
        Assert.Equal("app-1", result.ApplicationId);
        Assert.Equal("ACCEPTED", result.Status);
        Assert.Contains("не успела завершиться", result.Error);
        Assert.InRange(started.Elapsed.TotalSeconds, 0, 4);
        Assert.True(handler.Calls < 8);
    }

    /// <summary>
    /// total больше 10 страниц по 1000: список неполный и не считается успешно загруженным.
    /// </summary>
    [Fact]
    public async Task List_общий_объём_больше_потолка_не_завершён()
    {
        var handler = new CaptureHandler("""
            <root>
              <activityLocationList total="10001">
                <location>
                  <enterprise>
                    <guid>aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa</guid>
                    <name>Склад</name>
                  </enterprise>
                </location>
              </activityLocationList>
            </root>
            """);
        var gateway = new VetisGateway(new StubFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<VetisGateway>.Instance);

        var result = await gateway.ListEnterprisesAsync(new VetisCall
        {
            Endpoint = "https://vetis.example/ApplicationManagementService",
            BasicLogin = "user-1",
            BasicPassword = "secret-1",
            IssuerId = "issuer-1",
            TimeoutSeconds = 5
        }, CancellationToken.None);

        Assert.False(result.Completed);
        Assert.Contains("10001", result.Error);
        Assert.Equal(1, handler.Calls);
    }

    private const string IncomingResponse = """
        <root>
          <applicationId>app-2</applicationId>
          <status>COMPLETED</status>
          <vetDocumentList count="100" total="2">
            <vetDocument>
              <uuid>v-1</uuid>
              <issueNumber>123</issueNumber>
              <issueDate>2026-10-02</issueDate>
              <vetDStatus>CONFIRMED</vetDStatus>
              <vetDType>INCOMING</vetDType>
              <lastUpdateDate>2026-10-03T10:00:00+07:00</lastUpdateDate>
              <certifiedConsignment>
                <consignor><enterprise><name>Поставщик</name></enterprise></consignor>
                <consignee><enterprise><name>Магазин</name></enterprise></consignee>
                <batch><volume>12.5</volume><productItem><name>Колбаса</name></productItem></batch>
              </certifiedConsignment>
              <referencedDocument>
                <type>1</type><issueNumber>ТТН-5</issueNumber><issueDate>2026-10-01</issueDate><relationshipType>1</relationshipType>
              </referencedDocument>
              <statusChange><status>CONFIRMED</status><actualDateTime>2026-10-02T12:00:00+07:00</actualDateTime></statusChange>
              <statusChange><status>UTILIZED</status><actualDateTime>2026-10-29T06:15:00+07:00</actualDateTime></statusChange>
            </vetDocument>
            <vetDocument><uuid>v-2</uuid><vetDType>OUTGOING</vetDType></vetDocument>
            <vetDocument><uuid>v-3</uuid><vetDocumentType>INCOMING</vetDocumentType><vetDType>PRODUCTIVE</vetDType></vetDocument>
            <vetDocument><uuid>v-4</uuid><vetDType>TRANSPORT</vetDType></vetDocument>
            <vetDocument><uuid>v-5</uuid><vetDType>RETURNABLE</vetDType></vetDocument>
            <vetDocument>
              <uuid>v-6</uuid>
              <vetDType>INCOMING</vetDType>
              <vetDStatus>CONFIRMED</vetDStatus>
              <statusChange><status>CONFIRMED</status><actualDateTime>2026-10-02T12:00:00+07:00</actualDateTime></statusChange>
              <statusChange><status>FINALIZED</status><actualDateTime>2026-10-29T08:00:00+07:00</actualDateTime></statusChange>
            </vetDocument>
          </vetDocumentList>
        </root>
        """;

    /// <summary>
    /// В истории изменений есть и исходящие ВСД: их нет в списке, но они занимают место на странице.
    /// </summary>
    [Fact]
    public void ReadIncoming_читает_входящие_ВСД_с_накладной_и_пропускает_исходящие()
    {
        var rows = VetisResponseReader.ReadIncoming(IncomingResponse);
        var row = rows.Single(item => item.Uuid == "v-1");
        var issued = rows.Single(item => item.Uuid == "v-6");

        Assert.Equal("v-1", row.Uuid);
        Assert.Equal("123", row.IssueNumber);
        Assert.Equal("2026-10-02", row.IssueDate);
        Assert.Equal("CONFIRMED", row.Status);
        Assert.Equal("Колбаса", row.ProductName);
        Assert.Equal("12.5", row.Volume);
        Assert.Equal("Поставщик", row.ConsignorName);
        Assert.Equal("Магазин", row.ConsigneeName);
        Assert.Equal("ТТН-5", row.SourceNumber);
        Assert.Equal("2026-10-01", row.SourceDate);
        Assert.Equal("1", row.SourceType);
        Assert.Equal("2026-10-29T06:15:00+07:00", row.ReceivedAt);
        Assert.Equal("", issued.ReceivedAt);
        Assert.Equal("2026-10-03T10:00:00+07:00", row.UpdatedAt);
        Assert.Contains("<uuid>v-1</uuid>", row.RawXml);
        Assert.Equal(6, VetisResponseReader.ListedCount(IncomingResponse));
    }

    [Fact]
    public async Task GetIncoming_шлёт_окно_и_площадку_и_отдаёт_ВСД()
    {
        var handler = new CaptureHandler(IncomingResponse);
        var gateway = new VetisGateway(new StubFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<VetisGateway>.Instance);

        var result = await gateway.GetIncomingAsync(
            Call(),
            new VetisIncomingRequest
            {
                EnterpriseGuid = "enterprise-1",
                Begin = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero),
                Offset = 100,
                Count = 100
            },
            CancellationToken.None);

        Assert.True(result.Exchange.Completed);
        Assert.Equal("GetVetDocumentChangesList", result.Exchange.Operation);
        Assert.Equal(6, result.PageCount);
        Assert.Equal(["v-1", "v-6"], result.Documents.Select(item => item.Uuid));
        Assert.Contains("enterprise-1", handler.Body);
        Assert.Contains("2026-10-01T00:00:00+00:00", handler.Body);
        Assert.Contains("<bs:offset>100</bs:offset>", handler.Body);
    }

    [Fact]
    public async Task GetIncoming_кладёт_в_обмен_запрос_без_ключа_ответ_и_параметры()
    {
        var handler = new CaptureHandler(IncomingResponse);
        var gateway = new VetisGateway(new StubFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<VetisGateway>.Instance);

        var result = await gateway.GetIncomingAsync(
            Call(),
            new VetisIncomingRequest
            {
                EnterpriseGuid = "enterprise-1",
                Begin = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero),
                Offset = 100,
                Count = 100
            },
            CancellationToken.None);

        Assert.Contains("enterprise-1", result.Exchange.RequestXml);
        Assert.DoesNotContain("key-1", result.Exchange.RequestXml);
        Assert.Contains(SecretMask.Placeholder, result.Exchange.RequestXml);
        Assert.Contains("<uuid>v-1</uuid>", result.Exchange.ResponseXml);
        Assert.False(string.IsNullOrWhiteSpace(result.Exchange.LocalTransactionId));
        Assert.Contains("смещение 100", result.Exchange.Parameters);
        Assert.Contains("количество 100", result.Exchange.Parameters);
    }

    [Fact]
    public async Task GetIncoming_не_завершённая_заявка_не_отдаёт_ВСД()
    {
        var handler = new CaptureHandler("""
            <root><applicationId>app-3</applicationId><status>REJECTED</status><error>Недопустимый интервал</error></root>
            """);
        var gateway = new VetisGateway(new StubFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<VetisGateway>.Instance);

        var result = await gateway.GetIncomingAsync(
            Call(),
            new VetisIncomingRequest { EnterpriseGuid = "e1", Begin = DateTimeOffset.Now.AddDays(-1), End = DateTimeOffset.Now },
            CancellationToken.None);

        Assert.False(result.Exchange.Completed);
        Assert.Contains("интервал", result.Exchange.Error);
        Assert.Empty(result.Documents);
        Assert.Contains("<error>", result.Exchange.ResponseXml);
    }

    [Fact]
    public async Task Check_кладёт_в_обмен_запрос_без_ключа_и_ответ()
    {
        var handler = new CaptureHandler("""
            <root><applicationId>app-1</applicationId><status>COMPLETED</status></root>
            """);
        var gateway = new VetisGateway(new StubFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<VetisGateway>.Instance);

        var result = await gateway.CheckConnectionAsync(Call(), CancellationToken.None);

        Assert.Equal("GetBusinessEntityUser", result.Operation);
        Assert.DoesNotContain("key-1", result.RequestXml);
        Assert.Contains("operator-1", result.RequestXml);
        Assert.Contains("app-1", result.ResponseXml);
    }

    [Fact]
    public async Task Неотправленная_заявка_сохраняет_запрос_и_ошибку()
    {
        var gateway = new VetisGateway(new StubFactory(new FailingHandler()), Microsoft.Extensions.Logging.Abstractions.NullLogger<VetisGateway>.Instance);

        var result = await gateway.CheckConnectionAsync(Call(), CancellationToken.None);

        Assert.False(result.Completed);
        Assert.Equal("NOT_SENT", result.Status);
        Assert.Equal("GetBusinessEntityUser", result.Operation);
        Assert.Contains("operator-1", result.RequestXml);
        Assert.DoesNotContain("key-1", result.RequestXml);
    }

    [Fact]
    public async Task List_отдаёт_обмен_на_каждую_страницу()
    {
        var handler = new CaptureHandler("""
            <root>
              <activityLocationList total="1">
                <location><enterprise><guid>aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa</guid><name>Склад</name></enterprise></location>
              </activityLocationList>
            </root>
            """);
        var gateway = new VetisGateway(new StubFactory(handler), Microsoft.Extensions.Logging.Abstractions.NullLogger<VetisGateway>.Instance);

        var result = await gateway.ListEnterprisesAsync(Call(), CancellationToken.None);

        var exchange = Assert.Single(result.Exchanges);
        Assert.Equal("GetActivityLocationList", exchange.Operation);
        Assert.True(exchange.Completed);
        Assert.Contains("issuer-1", exchange.RequestXml);
        Assert.Contains("activityLocationList", exchange.ResponseXml);
        Assert.Contains("смещение 0", exchange.Parameters);
    }

    private static VetisCall Call() => new()
    {
        Endpoint = "https://vetis.example/ApplicationManagementService",
        ApiKey = "key-1",
        BasicLogin = "user-1",
        BasicPassword = "secret-1",
        IssuerId = "issuer-1",
        InitiatorLogin = "operator-1",
        ServiceId = VetisEndpoints.ServiceId,
        ResultPollAttempts = 1,
        TimeoutSeconds = 5
    };

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("нет сети");
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly string _response;

        public CaptureHandler(string response) => _response = response;

        public string Authorization { get; private set; } = "";

        public string SoapAction { get; private set; } = "";

        public string Body { get; private set; } = "";

        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Authorization = request.Headers.Authorization?.ToString() ?? "";
            SoapAction = request.Headers.TryGetValues("SOAPAction", out var values) ? values.First() : "";
            Body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "text/xml")
            };
        }
    }
}
