using System.Net;
using System.Text;
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
