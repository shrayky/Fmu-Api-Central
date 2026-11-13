using System.Net;
using System.Text;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using EtrnOfd;

namespace EtrnOfd.Tests;

public class OfdTokenClientTests
{
    [Fact]
    public async Task Вход_шлёт_присоединённую_подпись_и_повторно_не_ходит_до_истечения()
    {
        var handler = new TokenHandler(_ => Json(access: "access-1", expiresIn: 900));
        var now = new DateTimeOffset(2026, 11, 10, 13, 0, 0, TimeSpan.FromHours(7));
        var signer = new RecordingSigner();
        var client = Client(handler, signer, () => now, () => "nonce-1");

        var first = await client.AccessTokenAsync(Context(), CancellationToken.None);
        var second = await client.AccessTokenAsync(Context(), CancellationToken.None);

        Assert.True(first.IsSuccess, first.IsFailure ? first.Error : "");
        Assert.Equal("access-1", first.Value.Token);
        Assert.Equal("access-1", second.Value.Token);
        var call = Assert.Single(handler.Calls);
        Assert.Equal(
            "https://edo.platformaofd.ru/security/public/api/v3/oauth/token",
            call.Url);
        Assert.Equal("central", call.ClientId);
        var form = Form(call.Body);
        Assert.Equal("signature", form["grant_type"]);
        Assert.Equal("Q1NT", form["signature"]);
        Assert.False(form.ContainsKey("api_key"));
        Assert.Equal("serial", signer.CertificateNumber);
        Assert.Equal(
            $$"""{"ts":"{{now.ToUnixTimeSeconds()}}","nonce":"nonce-1"}""",
            Encoding.UTF8.GetString(signer.Content));
    }

    [Fact]
    public async Task После_истечения_подпись_запрашивается_снова()
    {
        var step = 0;
        var handler = new TokenHandler(_ =>
        {
            step++;
            return Json(access: "access-" + step, expiresIn: 10);
        });
        var now = new DateTimeOffset(2026, 11, 10, 13, 0, 0, TimeSpan.FromHours(7));
        var client = Client(handler, new RecordingSigner(), () => now, () => "nonce-" + step);

        var first = await client.AccessTokenAsync(Context(), CancellationToken.None);
        now = now.AddSeconds(10);
        var second = await client.AccessTokenAsync(Context(), CancellationToken.None);

        Assert.Equal("access-1", first.Value.Token);
        Assert.Equal("access-2", second.Value.Token);
        Assert.Equal(2, handler.Calls.Count);
        Assert.Equal("signature", Form(handler.Calls[1].Body)["grant_type"]);
    }

    [Fact]
    public async Task Проверка_подключения_берёт_новый_токен_мимо_кэша()
    {
        var handler = new TokenHandler(call => Json(access: "access-" + call, expiresIn: 900));
        var now = new DateTimeOffset(2026, 11, 10, 13, 0, 0, TimeSpan.FromHours(7));
        var client = Client(handler, new RecordingSigner(), () => now, () => "nonce");

        await client.AccessTokenAsync(Context(), CancellationToken.None);
        var fresh = await client.AccessTokenAsync(Context(), fresh: true, CancellationToken.None);

        Assert.Equal("access-2", fresh.Value.Token);
        Assert.Equal(2, handler.Calls.Count);
        Assert.Equal("signature", Form(handler.Calls[1].Body)["grant_type"]);
    }

    [Fact]
    public async Task Без_сертификата_запрос_не_уходит()
    {
        var handler = new TokenHandler(_ => Json(access: "access-1", expiresIn: 900));
        var signer = new RecordingSigner();
        var client = Client(handler, signer, () => DateTimeOffset.UnixEpoch, () => "nonce");
        var context = Context();
        context = new EtrnOperatorContext
        {
            Connection = context.Connection,
            CertificateNumber = "  "
        };

        var result = await client.AccessTokenAsync(context, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Не задан сертификат организации", result.Error);
        Assert.Empty(handler.Calls);
        Assert.Equal(0, signer.Calls);
    }

    [Fact]
    public async Task Ответ_без_токена_это_ошибка()
    {
        var handler = new TokenHandler(_ => """{"expires_in":900}""");
        var client = Client(handler, new RecordingSigner(), () => DateTimeOffset.UnixEpoch, () => "nonce");

        var result = await client.AccessTokenAsync(Context(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Платформа ЭТрН не вернула токен", result.Error);
    }

    [Fact]
    public async Task Отказ_токена_показывает_текст_площадки()
    {
        var handler = new TokenHandler(_ => """{"error":"invalid_request","error_description":"Signature not valid or corrupted"}""")
        {
            StatusCode = HttpStatusCode.BadRequest
        };
        var client = Client(handler, new RecordingSigner(), () => DateTimeOffset.UnixEpoch, () => "nonce");

        var result = await client.AccessTokenAsync(Context(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Платформа ЭТрН не выдала токен: Signature not valid or corrupted", result.Error);
    }

    private static OfdTokenClient Client(
        TokenHandler handler,
        RecordingSigner signer,
        Func<DateTimeOffset> clock,
        Func<string> nonce) =>
        new(new HttpClient(handler, disposeHandler: false), signer, new OfdTokenStore(), clock, nonce);

    private static EtrnOperatorContext Context() => new()
    {
        CertificateNumber = "serial",
        CertificatePassword = "pwd",
        Connection = new EtrnConnection
        {
            Id = "c1",
            Type = EtrnOperatorType.Ofd,
            ApiUrl = "https://api.platformaofd.ru"
        }
    };

    private static string Json(string access, int expiresIn) =>
        $$"""{"access_token":"{{access}}","expires_in":{{expiresIn}}}""";

    private static Dictionary<string, string> Form(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => Uri.UnescapeDataString(pair[1].Replace("+", " ")));

    private sealed class RecordingSigner : IOfdAttachedSignature
    {
        public int Calls { get; private set; }

        public string CertificateNumber { get; private set; } = "";

        public byte[] Content { get; private set; } = [];

        public Task<Result<string>> SignAsync(
            string certificateNumber,
            string? password,
            byte[] content,
            CancellationToken cancellationToken)
        {
            Calls++;
            CertificateNumber = certificateNumber;
            Content = content;
            return Task.FromResult(Result.Success("Q1NT"));
        }
    }

    private sealed class TokenHandler : HttpMessageHandler
    {
        private readonly Func<int, string> _body;
        private int _count;

        public TokenHandler(Func<int, string> body) => _body = body;

        public List<TokenCall> Calls { get; } = [];

        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("x-client-id", out var clientId);
            Calls.Add(new TokenCall(request.RequestUri?.ToString() ?? "", body, clientId?.SingleOrDefault()));
            _count++;
            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(_body(_count), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record TokenCall(string Url, string Body, string? ClientId);
}
