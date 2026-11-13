using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using Microsoft.Extensions.DependencyInjection;

namespace EtrnOfd;

public sealed class OfdTokenClient
{
    public const string ClientIdHeader = "central";
    public const string TokenPath = "/security/public/api/v3/oauth/token";

    private readonly HttpClient _http;
    private readonly IOfdAttachedSignature _signer;
    private readonly OfdTokenStore _store;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<string> _nonce;

    [ActivatorUtilitiesConstructor]
    public OfdTokenClient(HttpClient http, IOfdAttachedSignature signer, OfdTokenStore store)
        : this(http, signer, store, () => DateTimeOffset.Now, () => Guid.NewGuid().ToString())
    {
    }

    public OfdTokenClient(
        HttpClient http,
        IOfdAttachedSignature signer,
        OfdTokenStore store,
        Func<DateTimeOffset> clock,
        Func<string> nonce)
    {
        _http = http;
        _signer = signer;
        _store = store;
        _now = clock;
        _nonce = nonce;
    }

    public Task<Result<OfdAccess>> AccessTokenAsync(EtrnOperatorContext context, CancellationToken cancellationToken) =>
        AccessTokenAsync(context, fresh: false, cancellationToken);

    public async Task<Result<OfdAccess>> AccessTokenAsync(
        EtrnOperatorContext context,
        bool fresh,
        CancellationToken cancellationToken)
    {
        var connection = context.Connection;
        var missing = connection.MissingOfdAddress();
        if (missing.Length > 0)
            return Result.Failure<OfdAccess>(missing);
        if (string.IsNullOrWhiteSpace(context.CertificateNumber))
            return Result.Failure<OfdAccess>("Не задан сертификат организации");

        var connectionId = connection.Id ?? "";
        if (!fresh && _store.TryGet(connectionId, _now(), out var cached))
            return Result.Success(new OfdAccess(cached, 0));

        try
        {
            var signed = await SignAsync(context, cancellationToken);
            if (signed.IsFailure)
                return Result.Failure<OfdAccess>(signed.Error);

            var logged = await RequestAsync(connection, signed.Value, cancellationToken);
            if (logged.IsFailure)
                return Result.Failure<OfdAccess>(logged.Error);

            Remember(connectionId, logged.Value);
            return Result.Success(new OfdAccess(logged.Value.AccessToken, logged.Value.ExpiresIn));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return Result.Failure<OfdAccess>($"Платформа ЭТрН не выдала токен: {ex.Message}");
        }
    }

    /// <summary>
    /// 1С пишет ts строкой: ЗаписатьЗначение от текста Unix-времени.
    /// </summary>
    public static string ChallengeJson(DateTimeOffset now, string nonce) =>
        $"{{\"ts\":\"{now.ToUnixTimeSeconds()}\",\"nonce\":\"{nonce}\"}}";

    private async Task<Result<string>> SignAsync(EtrnOperatorContext context, CancellationToken cancellationToken)
    {
        var json = ChallengeJson(_now(), _nonce());
        return await _signer.SignAsync(
            context.CertificateNumber,
            context.CertificatePassword,
            Encoding.UTF8.GetBytes(json),
            cancellationToken);
    }

    private void Remember(string connectionId, OfdTokenResponse response) =>
        _store.Remember(connectionId, response.AccessToken, "", response.ExpiresIn, _now());

    private async Task<Result<OfdTokenResponse>> RequestAsync(
        EtrnConnection connection,
        string signature,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, connection.ResolvedEdoUrl() + TokenPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "signature",
                ["signature"] = signature
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("x-client-id", ClientIdHeader);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var failed = await response.Content.ReadAsStringAsync(cancellationToken);
            return Result.Failure<OfdTokenResponse>($"Платформа ЭТрН не выдала токен: {TokenError(failed, (int)response.StatusCode)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var access = Text(root, "access_token");
        if (access.Length == 0)
            return Result.Failure<OfdTokenResponse>("Платформа ЭТрН не вернула токен");

        var expires = root.TryGetProperty("expires_in", out var expiresIn) && expiresIn.TryGetInt32(out var seconds)
            ? seconds
            : 0;
        return Result.Success(new OfdTokenResponse(access, expires));
    }

    private static string TokenError(string body, int status)
    {
        var plain = (body ?? "").Trim();
        if (!plain.StartsWith('{'))
            return plain.Length == 0 ? status.ToString() : plain;

        using var json = JsonDocument.Parse(plain);
        var text = Text(json.RootElement, "error_description");
        if (text.Length == 0)
            text = Text(json.RootElement, "error");
        return text.Length == 0 ? status.ToString() : text;
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? "").Trim()
            : "";

    private sealed record OfdTokenResponse(string AccessToken, int ExpiresIn);
}

public sealed record OfdAccess(string Token, int ExpiresIn);
