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

    public async Task<Result<string>> AccessTokenAsync(
        string connectionId,
        EtrnSettings settings,
        string apiKey,
        CancellationToken cancellationToken)
    {
        if (_store.TryGet(connectionId, _now(), out var cached))
            return Result.Success(cached);

        try
        {
            var refresh = _store.RefreshToken(connectionId);
            if (refresh.Length > 0)
            {
                var refreshed = await RequestAsync(settings, RefreshForm(settings, refresh), cancellationToken);
                if (refreshed.IsSuccess)
                {
                    Remember(connectionId, refreshed.Value);
                    return Result.Success(refreshed.Value.AccessToken);
                }
            }

            var logged = await RequestAsync(settings, PasswordForm(settings, apiKey), cancellationToken);
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
        EtrnSettings settings,
        IEnumerable<KeyValuePair<string, string>> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl(settings))
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

    private List<KeyValuePair<string, string>> PasswordForm(EtrnSettings settings, string apiKey)
    {
        var form = BaseForm(settings);
        form.Add(new KeyValuePair<string, string>("grant_type", "password"));
        form.Add(new KeyValuePair<string, string>("api_key", _hash.Hex(apiKey)));
        return form;
    }

    private static List<KeyValuePair<string, string>> RefreshForm(EtrnSettings settings, string refreshToken)
    {
        var form = BaseForm(settings);
        form.Add(new KeyValuePair<string, string>("grant_type", "refresh_token"));
        form.Add(new KeyValuePair<string, string>("refresh_token", refreshToken));
        return form;
    }

    private static List<KeyValuePair<string, string>> BaseForm(EtrnSettings settings)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("client_id", (settings.OfdClientId ?? "").Trim())
        };
        var secret = settings.OfdClientSecret ?? "";
        if (!string.IsNullOrWhiteSpace(secret))
            form.Add(new KeyValuePair<string, string>("client_secret", secret));
        return form;
    }

    private static string TokenUrl(EtrnSettings settings)
    {
        var root = (settings.OfdKeycloakUrl ?? "").Trim().TrimEnd('/');
        var realm = (settings.OfdRealm ?? "").Trim();
        return $"{root}/realms/{realm}/protocol/openid-connect/token";
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? "").Trim()
            : "";

    private sealed record OfdTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
}
