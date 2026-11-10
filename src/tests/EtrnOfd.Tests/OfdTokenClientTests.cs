using System.Net;
using System.Text;
using Domain.Etrn;
using EtrnOfd;

namespace EtrnOfd.Tests;

public class OfdTokenClientTests
{
    [Fact]
    public async Task Вход_шлёт_хеш_ключа_и_повторно_не_ходит_до_истечения()
    {
        var handler = new TokenHandler(_ => Json(access: "access-1", refresh: "refresh-1", expiresIn: 900));
        var now = new DateTimeOffset(2026, 11, 10, 13, 0, 0, TimeSpan.FromHours(7));
        var client = Client(handler, () => now);

        var first = await client.AccessTokenAsync("c1", Settings(""), "plain-key", CancellationToken.None);
        var second = await client.AccessTokenAsync("c1", Settings(""), "plain-key", CancellationToken.None);

        Assert.True(first.IsSuccess, first.IsFailure ? first.Error : "");
        Assert.Equal("access-1", first.Value);
        Assert.Equal("access-1", second.Value);
        var call = Assert.Single(handler.Calls);
        Assert.Equal(
            "https://oauth-test.platformaofd.ru/realms/playground/protocol/openid-connect/token",
            call.Url);
        Assert.Null(call.Authorization);
        var form = Form(call.Body);
        Assert.Equal("password", form["grant_type"]);
        Assert.Equal("central", form["client_id"]);
        Assert.Equal("AB", form["api_key"]);
        Assert.DoesNotContain("plain-key", call.Body);
        Assert.False(form.ContainsKey("client_secret"));
    }

    [Fact]
    public async Task Секрет_клиента_уходит_только_когда_он_задан()
    {
        var handler = new TokenHandler(_ => Json(access: "access-1", refresh: "refresh-1", expiresIn: 900));
        var client = Client(handler, () => DateTimeOffset.UnixEpoch);

        var result = await client.AccessTokenAsync("c1", Settings("s3cret"), "plain-key", CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.Equal("s3cret", Form(Assert.Single(handler.Calls).Body)["client_secret"]);
    }

    [Fact]
    public async Task Ответ_без_токена_это_ошибка()
    {
        var handler = new TokenHandler(_ => """{"expires_in":900}""");
        var client = Client(handler, () => DateTimeOffset.UnixEpoch);

        var result = await client.AccessTokenAsync("c1", Settings(""), "plain-key", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ОФД не вернула токен", result.Error);
    }

    [Fact]
    public async Task После_истечения_сначала_обновление_а_при_отказе_один_новый_вход()
    {
        var step = 0;
        var handler = new TokenHandler(_ =>
        {
            step++;
            return step switch
            {
                1 => Json(access: "access-1", refresh: "refresh-1", expiresIn: 10),
                2 => "",
                _ => Json(access: "access-2", refresh: "refresh-2", expiresIn: 10)
            };
        });
        handler.Status = call => call == 2 ? HttpStatusCode.BadRequest : HttpStatusCode.OK;
        var now = new DateTimeOffset(2026, 11, 10, 13, 0, 0, TimeSpan.FromHours(7));
        var client = Client(handler, () => now);

        var first = await client.AccessTokenAsync("c1", Settings(""), "plain-key", CancellationToken.None);
        now = now.AddSeconds(10);
        var second = await client.AccessTokenAsync("c1", Settings(""), "plain-key", CancellationToken.None);

        Assert.Equal("access-1", first.Value);
        Assert.Equal("access-2", second.Value);
        Assert.Equal(3, handler.Calls.Count);
        Assert.Equal("refresh_token", Form(handler.Calls[1].Body)["grant_type"]);
        Assert.Equal("refresh-1", Form(handler.Calls[1].Body)["refresh_token"]);
        Assert.False(Form(handler.Calls[1].Body).ContainsKey("api_key"));
        Assert.Equal("password", Form(handler.Calls[2].Body)["grant_type"]);
    }

    private static OfdTokenClient Client(TokenHandler handler, Func<DateTimeOffset> clock) =>
        new(new HttpClient(handler, disposeHandler: false), new FixedHash("AB"), new OfdTokenStore(), clock);

    private static EtrnSettings Settings(string secret) => new()
    {
        OfdClientId = "central",
        OfdClientSecret = secret,
        OfdKeycloakUrl = EtrnSettings.DefaultKeycloakUrl,
        OfdRealm = EtrnSettings.DefaultRealm
    };

    private static string Json(string access, string refresh, int expiresIn) =>
        $$"""{"access_token":"{{access}}","refresh_token":"{{refresh}}","expires_in":{{expiresIn}}}""";

    private static Dictionary<string, string> Form(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => Uri.UnescapeDataString(pair[1].Replace("+", " ")));

    private sealed class FixedHash(string hex) : IOfdApiKeyHash
    {
        public string Hex(string apiKey) => hex;
    }

    private sealed class TokenHandler : HttpMessageHandler
    {
        private readonly Func<int, string> _body;
        private int _count;

        public TokenHandler(Func<int, string> body) => _body = body;

        public List<TokenCall> Calls { get; } = [];

        public Func<int, HttpStatusCode> Status { get; set; } = _ => HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var authorization = request.Headers.Authorization?.ToString();
            Calls.Add(new TokenCall(request.RequestUri?.ToString() ?? "", body, authorization));
            _count++;
            return new HttpResponseMessage(Status(_count))
            {
                Content = new StringContent(_body(_count), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record TokenCall(string Url, string Body, string? Authorization);
}
