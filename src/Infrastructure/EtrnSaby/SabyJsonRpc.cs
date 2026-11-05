using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EtrnSaby;

public sealed class SabyRpcAnswer
{
    public bool Ok { get; init; }

    public JsonElement? Result { get; init; }

    public string ResultText { get; init; } = "";

    public string RequestBody { get; init; } = "";

    public string RawBody { get; init; } = "";

    public string Error { get; init; } = "";

    public bool SessionExpired { get; init; }
}

public sealed class SabyJsonRpc
{
    public const string HttpClientName = "EtrnSaby";

    public const string AuthUrl = "https://online.sbis.ru/auth/service/";

    public const string ServiceUrl = "https://online.sbis.ru/service/?srv=1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SabyJsonRpc> _logger;

    public SabyJsonRpc(IHttpClientFactory httpClientFactory, ILogger<SabyJsonRpc> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<SabyRpcAnswer> SendAsync(
        string url,
        string method,
        object? parameters,
        string? sessionId,
        string userAgent,
        CancellationToken cancellationToken)
    {
        var body = BuildBody(method, parameters);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json-rpc");
        request.Content.Headers.ContentType!.CharSet = "utf-8";
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (!string.IsNullOrWhiteSpace(sessionId))
            request.Headers.TryAddWithoutValidation("X-SBISSessionID", sessionId);

        var raw = "";
        try
        {
            using var http = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await http.SendAsync(request, cancellationToken);
            raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new SabyRpcAnswer
                {
                    RequestBody = body,
                    RawBody = raw,
                    Error = $"Saby вернула {(int)response.StatusCode}: {Compact(raw)}"
                };
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            _logger.LogError(ex, "Ошибка вызова Saby {Method}", method);
            return new SabyRpcAnswer
            {
                RequestBody = body,
                Error = $"Нет связи с Saby: {ex.Message}"
            };
        }

        return Parse(method, body, raw);
    }

    private static string BuildBody(string method, object? parameters)
    {
        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["id"] = Guid.NewGuid().ToString("N")
        };
        if (parameters != null)
            payload["params"] = parameters;

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static SabyRpcAnswer Parse(string method, string body, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new SabyRpcAnswer { RequestBody = body, Error = "Пустой ответ Saby." };

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error))
            {
                var message = ReadError(error);
                return new SabyRpcAnswer
                {
                    RequestBody = body,
                    RawBody = raw,
                    Error = message,
                    SessionExpired = SabySession.IsExpired(message)
                };
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("result", out var result))
            {
                // Некоторые методы отдают result строкой с вложенным JSON.
                if (result.ValueKind == JsonValueKind.String)
                {
                    var text = result.GetString() ?? "";
                    if (text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('['))
                    {
                        try
                        {
                            using var nested = JsonDocument.Parse(text);
                            return new SabyRpcAnswer
                            {
                                Ok = true,
                                Result = nested.RootElement.Clone(),
                                ResultText = text,
                                RequestBody = body,
                                RawBody = raw
                            };
                        }
                        catch (JsonException)
                        {
                            // Оставляем строку как есть.
                        }
                    }
                }

                return new SabyRpcAnswer
                {
                    Ok = true,
                    Result = result.Clone(),
                    ResultText = result.ValueKind == JsonValueKind.String ? result.GetString() ?? "" : result.GetRawText(),
                    RequestBody = body,
                    RawBody = raw
                };
            }

            return new SabyRpcAnswer
            {
                RequestBody = body,
                RawBody = raw,
                Error = $"Saby не вернула result в ответе на {method}."
            };
        }
        catch (JsonException)
        {
            return new SabyRpcAnswer
            {
                RequestBody = body,
                RawBody = raw,
                Error = "Ответ Saby не разобран как JSON."
            };
        }
    }

    private static string ReadError(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.String)
            return error.GetString() ?? "Ошибка Saby";

        if (error.ValueKind != JsonValueKind.Object)
            return error.GetRawText();

        foreach (var name in new[] { "message", "Message", "error", "Описание" })
        {
            if (error.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? "Ошибка Saby";
        }

        return error.GetRawText();
    }

    private static string Compact(string value)
    {
        var text = value.ReplaceLineEndings(" ").Trim();
        return text.Length <= 500 ? text : text[..500];
    }
}

public static class SabySession
{
    public static bool IsExpired(string? message)
    {
        var text = (message ?? "").ToLowerInvariant();
        if (!text.Contains("сесси") && !text.Contains("session"))
            return false;

        return text.Contains("истек")
               || text.Contains("не найд")
               || text.Contains("законч")
               || text.Contains("прерван")
               || text.Contains("просроч")
               || text.Contains("expired")
               || text.Contains("invalid")
               || text.Contains("closed");
    }
}
