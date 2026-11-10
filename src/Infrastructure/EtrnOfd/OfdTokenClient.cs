using System.Net.Http.Headers;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using Microsoft.Extensions.DependencyInjection;

namespace EtrnOfd;

public sealed class OfdTokenClient
{
    private readonly HttpClient _http;
    private readonly IOfdApiKeyHash _hash;
    private readonly OfdTokenStore _store;
    private readonly Func<DateTimeOffset> _now;

    [ActivatorUtilitiesConstructor]
    public OfdTokenClient(HttpClient http, IOfdApiKeyHash hash, OfdTokenStore store)
        : this(http, hash, store, () => DateTimeOffset.Now)
    {
    }

    public OfdTokenClient(HttpClient http, IOfdApiKeyHash hash, OfdTokenStore store, Func<DateTimeOffset> clock)
    {
        _http = http;
        _hash = hash;
        _store = store;
        _now = clock;
    }

    public Task<Result<string>> AccessTokenAsync(EtrnConnection connection, CancellationToken cancellationToken) =>
        AccessTokenAsync(connection, fresh: false, cancellationToken);

    public async Task<Result<string>> AccessTokenAsync(
        EtrnConnection connection,
        bool fresh,
        CancellationToken cancellationToken)
    {
        var connectionId = connection.Id ?? "";
        if (!fresh && _store.TryGet(connectionId, _now(), out var cached))
            return Result.Success(cached);

        try
        {
            var refresh = fresh ? "" : _store.RefreshToken(connectionId);
            if (refresh.Length > 0)
            {
                var refreshed = await RequestAsync(connection, RefreshForm(connection, refresh), cancellationToken);
                if (refreshed.IsSuccess)
                {
                    Remember(connectionId, refreshed.Value);
                    return Result.Success(refreshed.Value.AccessToken);
                }
            }

            var logged = await RequestAsync(connection, PasswordForm(connection), cancellationToken);
            if (logged.IsFailure)
                return Result.Failure<string>(logged.Error);

            Remember(connectionId, logged.Value);
            return Result.Success(logged.Value.AccessToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return Result.Failure<string>($"ОФД не выдала токен: {ex.Message}");
        }
    }

    private void Remember(string connectionId, OfdTokenResponse response) =>
        _store.Remember(connectionId, response.AccessToken, response.RefreshToken, response.ExpiresIn, _now());

    private async Task<Result<OfdTokenResponse>> RequestAsync(
        EtrnConnection connection,
        IEnumerable<KeyValuePair<string, string>> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl(connection))
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return Result.Failure<OfdTokenResponse>($"ОФД не выдала токен: {(int)response.StatusCode}");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var access = Text(root, "access_token");
        if (access.Length == 0)
            return Result.Failure<OfdTokenResponse>("ОФД не вернула токен");

        var expires = root.TryGetProperty("expires_in", out var expiresIn) && expiresIn.TryGetInt32(out var seconds)
            ? seconds
            : 0;
        return Result.Success(new OfdTokenResponse(access, Text(root, "refresh_token"), expires));
    }

    private List<KeyValuePair<string, string>> PasswordForm(EtrnConnection connection)
    {
        var form = BaseForm(connection);
        form.Add(new KeyValuePair<string, string>("grant_type", "password"));
        form.Add(new KeyValuePair<string, string>("api_key", _hash.Hex(connection.ApiKey)));
        return form;
    }

    private static List<KeyValuePair<string, string>> RefreshForm(EtrnConnection connection, string refreshToken)
    {
        var form = BaseForm(connection);
        form.Add(new KeyValuePair<string, string>("grant_type", "refresh_token"));
        form.Add(new KeyValuePair<string, string>("refresh_token", refreshToken));
        return form;
    }

    private static List<KeyValuePair<string, string>> BaseForm(EtrnConnection connection)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("client_id", (connection.ClientId ?? "").Trim())
        };
        var secret = connection.ClientSecret ?? "";
        if (!string.IsNullOrWhiteSpace(secret))
            form.Add(new KeyValuePair<string, string>("client_secret", secret));
        return form;
    }

    private static string TokenUrl(EtrnConnection connection) =>
        $"{connection.ResolvedKeycloakUrl()}/realms/{connection.ResolvedRealm()}/protocol/openid-connect/token";

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? "").Trim()
            : "";

    private sealed record OfdTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
}
