using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration;

public static class TrueApiPermissiveAccess
{
    public static TrueApiPermissiveAccessBody Body(string signedData)
        => new() { Data = signedData };

    /// <summary>
    /// LiveUntil в локальном времени вызывающего now: кэш сравнивает его с DateTime.Now по тикам.
    /// expiresIn — секунды из ответа CDN.
    /// </summary>
    public static Result<TrueApiSession> Parse(string? accessToken, int expiresIn, DateTime now)
    {
        var token = (accessToken ?? string.Empty).Trim();
        if (token.Length == 0)
            return Result.Failure<TrueApiSession>("True API не вернул x-api-key");

        if (expiresIn < 0)
            return Result.Failure<TrueApiSession>("True API вернул некорректный срок действия x-api-key");

        return Result.Success(new TrueApiSession(token, now.AddSeconds(expiresIn)));
    }
}

public sealed class TrueApiPermissiveAccessBody
{
    [JsonPropertyName("data")]
    public string Data { get; init; } = string.Empty;
}
