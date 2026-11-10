using System.Net;
using System.Text;
using CSharpFunctionalExtensions;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Etrn;
using Domain.Etrn.Xml;
using EtrnOfd;

namespace EtrnOfd.Tests;

public class OfdEtrnGatewayTests
{
    private const string TitleXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Файл ВерсФорм="5.01">
          <Документ КНД="1110339">
            <СодИнфГО>
              <СвГруз><ОпГруз НомерГруз="1" НаимГруз="Ящики" КолМестГр="2" ВесГруз="10"/></СвГруз>
            </СодИнфГО>
          </Документ>
        </Файл>
        """;

    [Fact]
    public async Task Опрос_берёт_только_груз_в_пути_и_не_пишет_токен_в_журнал()
    {
        var world = World.Ready();
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/protocol/openid-connect/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Json("""
                    [
                      {"id":308924,"uid":"uid-card","status":"CARGO_ON_WAY","number":"API-20","destAddress":{"postCode":"352570","regionCode":"23","district":"Мостовский р-н","settlement":"пгт Мостовской","street":"ул Николая Свистунова","building":"3"}},
                      {"id":400,"status":"CARGO_DELIVERED","number":"закрыта"}
                    ]
                    """);
            if (call.Url.EndsWith("/etrns/308924/documents", StringComparison.Ordinal))
                return OfdHandler.Json("""[{"id":1569904,"knd":"1110339","formVersion":"5.01"}]""");
            if (call.Url.EndsWith("/documents/1569904/content", StringComparison.Ordinal))
                return OfdHandler.Xml(TitleXml);
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal(world.Context.Position, result.Value.Position);
        Assert.Equal(["400"], result.Value.ClosedDocumentIds);
        var document = Assert.Single(result.Value.Documents);
        Assert.Equal("308924", document.OperatorDocumentId);
        Assert.Equal("API-20", document.Number);
        Assert.Equal("uid-card", document.Uid);
        Assert.Equal("352570, 23, Мостовский р-н, пгт Мостовской, ул Николая Свистунова, 3", document.DeliveryAddress);
        Assert.Equal(2, Assert.Single(document.Lines).Places);
        var list = Assert.Single(world.Handler.Calls, call => call.Url.Contains("/etrns?", StringComparison.Ordinal));
        Assert.Contains("subscriber-id=2775225", list.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("number=", list.Url, StringComparison.Ordinal);
        Assert.Equal("Bearer token-1", list.Authorization);
        Assert.DoesNotContain(world.Handler.Calls, call => call.Url.Contains("/etrns/400", StringComparison.Ordinal));
        Assert.Equal("application/xml", world.Handler.Calls.Single(call => call.Url.Contains("/content", StringComparison.Ordinal)).Accept);
        Assert.DoesNotContain(result.Value.Exchanges, exchange =>
            exchange.RequestBody.Contains("token-1", StringComparison.Ordinal)
            || exchange.ResponseBody.Contains("token-1", StringComparison.Ordinal)
            || exchange.RequestBody.Contains("plain-key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Список_в_поле_content_тоже_читается()
    {
        var world = World.Ready();
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/protocol/openid-connect/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Json("""{"content":[{"id":7,"status":"CARGO_RECEIVED"}]}""");
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal(["7"], result.Value.ClosedDocumentIds);
        Assert.Empty(result.Value.Documents);
    }

    [Fact]
    public async Task Чужой_корень_списка_не_сдвигает_пару_Saby()
    {
        var world = World.Ready();
        world.Handler.Respond = call => call.Url.Contains("/protocol/openid-connect/token", StringComparison.Ordinal)
            ? OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""")
            : OfdHandler.Json("""{"items":[]}""");

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal("ОФД вернула не список ЭТрН", result.Value.Error);
        Assert.Equal(world.Context.Position, result.Value.Position);
        Assert.Empty(result.Value.ClosedDocumentIds);
    }

    [Fact]
    public async Task Обрыв_второго_документа_оставляет_первый_и_не_закрывает_хвост()
    {
        var world = World.Ready();
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/protocol/openid-connect/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Json("""
                    [
                      {"id":1,"status":"CARGO_ON_WAY","number":"первый"},
                      {"id":2,"status":"CARGO_ON_WAY","number":"второй"}
                    ]
                    """);
            if (call.Url.EndsWith("/etrns/1/documents", StringComparison.Ordinal))
                return OfdHandler.Json("""[{"id":11,"knd":"1110339"}]""");
            if (call.Url.EndsWith("/documents/11/content", StringComparison.Ordinal))
                return OfdHandler.Xml(TitleXml);
            if (call.Url.EndsWith("/etrns/2/documents", StringComparison.Ordinal))
                return OfdHandler.Status(HttpStatusCode.InternalServerError);
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal("1", Assert.Single(result.Value.Documents).OperatorDocumentId);
        Assert.DoesNotContain("2", result.Value.ClosedDocumentIds);
        Assert.Equal(world.Context.Position, result.Value.Position);
    }

    [Fact]
    public async Task Без_абонента_и_ключа_токен_не_запрашивается()
    {
        var world = World.Ready();
        world.Context = Context(subscriberId: "", apiKey: "plain-key");
        var noSubscriber = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);
        world.Context = Context(subscriberId: "2775225", apiKey: "");
        var noKey = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.Equal("Не указан абонент ЭДО", noSubscriber.Value.Error);
        Assert.Equal("Не задан API-ключ Платформы ОФД", noKey.Value.Error);
        Assert.Empty(world.Handler.Calls);
    }

    [Fact]
    public async Task Приёмка_кладёт_XML_и_затем_байты_подписи()
    {
        var world = World.Ready();
        var signature = new byte[] { 1, 2, 3 };
        world.Signer.Signature = Convert.ToBase64String(signature);
        world.Handler.Respond = call => call.Url.Contains("/protocol/openid-connect/token", StringComparison.Ordinal)
            ? OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""")
            : OfdHandler.Json("""{"status":"ok"}""");
        var document = new EtrnIncomingDocumentEntity
        {
            OperatorDocumentId = "308924",
            Number = "ТН-77",
            Date = "2026-11-04",
            Uid = "uid-1",
            Lines = [new EtrnCargoLine { Index = 1, Places = 10, GrossWeight = "100" }]
        };
        var decision = new EtrnDecision { Action = EtrnAcceptanceAction.Received };

        var result = await world.Gateway.ExecuteAsync(
            world.Context,
            new EtrnExecuteCommand { Document = document, Decision = decision },
            CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        var puts = world.Handler.Calls.Where(call => call.Method == "PUT").ToList();
        Assert.Equal(2, puts.Count);
        Assert.EndsWith("/etrns/308924/documents/1110341v5.01", puts[0].Url, StringComparison.Ordinal);
        Assert.StartsWith("application/xml", puts[0].ContentType, StringComparison.Ordinal);
        var xml = ConsigneeTitleWriter.ToUtf8(ConsigneeTitleWriter.Build(document, decision, World.SignedAt));
        Assert.Equal(xml, puts[0].Body);
        Assert.Equal(xml, world.Signer.Last.Content);
        Assert.Equal("", world.Signer.Last.Hash);
        Assert.Equal("", world.Signer.Last.FileUrl);
        Assert.EndsWith("/documents/1110341v5.01/signature", puts[1].Url, StringComparison.Ordinal);
        Assert.StartsWith("application/octet-stream", puts[1].ContentType, StringComparison.Ordinal);
        Assert.Equal(signature, puts[1].Body);
        Assert.DoesNotContain(result.Value.Exchanges, exchange =>
            exchange.RequestBody.Contains("token-1", StringComparison.Ordinal)
            || exchange.ResponseBody.Contains("openid-connect", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Сверка_считает_приёмкой_только_груз_в_пути()
    {
        var world = World.Ready();
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/protocol/openid-connect/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            return OfdHandler.Json(call.Url.EndsWith("/etrns/1", StringComparison.Ordinal)
                ? """{"id":1,"status":"CARGO_ON_WAY"}"""
                : """{"id":2,"status":"CARGO_RECEIVED"}""");
        };

        var waiting = await world.Gateway.ReadStageAsync(world.Context, "1", CancellationToken.None);
        var closed = await world.Gateway.ReadStageAsync(world.Context, "2", CancellationToken.None);

        Assert.True(waiting.Value.StillAcceptance);
        Assert.Equal("CARGO_ON_WAY", waiting.Value.Stage);
        Assert.False(closed.Value.StillAcceptance);
    }

    private static EtrnOperatorContext Context(string subscriberId, string apiKey) => new()
    {
        OrganizationId = "o1",
        Inn = "7700000000",
        CertificateNumber = "serial",
        CertificatePassword = "pwd",
        Position = new EtrnPollPosition("e-old", "d-old"),
        Connection = new EtrnConnection
        {
            Id = "c1",
            Type = EtrnOperatorType.Ofd,
            Name = "ОФД",
            Enable = true,
            SubscriberId = subscriberId,
            ApiKey = apiKey
        }
    };

    private sealed class World
    {
        public static readonly DateTimeOffset SignedAt = new(2026, 11, 10, 13, 5, 0, TimeSpan.FromHours(7));

        public OfdHandler Handler { get; }
        public RecordingSigner Signer { get; }
        public OfdEtrnGateway Gateway { get; }
        public EtrnOperatorContext Context { get; set; } = OfdEtrnGatewayTests.Context("2775225", "plain-key");

        private World(OfdHandler handler, RecordingSigner signer, OfdEtrnGateway gateway)
        {
            Handler = handler;
            Signer = signer;
            Gateway = gateway;
        }

        public static World Ready()
        {
            var handler = new OfdHandler();
            var signer = new RecordingSigner();
            var tokens = new OfdTokenClient(
                new HttpClient(handler, disposeHandler: false),
                new FixedHash("AB"),
                new OfdTokenStore());
            var gateway = new OfdEtrnGateway(
                new SingleClientFactory(handler),
                tokens,
                signer,
                new FixedParameters(),
                () => SignedAt);
            return new World(handler, signer, gateway);
        }
    }

    private sealed class FixedParameters : IParametersService
    {
        public Task<Parameters> Current() => Task.FromResult(new Parameters());
        public Task<bool> Update(Parameters parameters) => Task.FromResult(true);
    }

    private sealed class FixedHash(string hex) : IOfdApiKeyHash
    {
        public string Hex(string apiKey) => hex;
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingSigner : IEtrnSignaturePort
    {
        public string Signature { get; set; } = Convert.ToBase64String([9]);
        public EtrnSignatureRequest Last { get; private set; } = new();

        public Task<Result<string>> SignAsync(EtrnSignatureRequest request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(Result.Success(Signature));
        }
    }

    private sealed record OfdCall(string Method, string Url, byte[] Body, string ContentType, string? Authorization, string Accept);

    private sealed class OfdHandler : HttpMessageHandler
    {
        public List<OfdCall> Calls { get; } = [];
        public Func<OfdCall, HttpResponseMessage> Respond { get; set; } = _ => Status(HttpStatusCode.NotFound);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var call = new OfdCall(
                request.Method.Method,
                request.RequestUri?.ToString() ?? "",
                body,
                request.Content?.Headers.ContentType?.MediaType ?? "",
                request.Headers.Authorization?.ToString(),
                request.Headers.Accept.ToString());
            Calls.Add(call);
            return Respond(call);
        }

        public static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        public static HttpResponseMessage Xml(string xml) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml")
        };

        public static HttpResponseMessage Status(HttpStatusCode status) => new(status);
    }
}
