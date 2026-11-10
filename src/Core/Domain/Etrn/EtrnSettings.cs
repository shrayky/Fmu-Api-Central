using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

namespace Domain.Etrn;

public class EtrnSettings
{
    public const string DefaultKeycloakUrl = "https://oauth-test.platformaofd.ru";
    public const string DefaultRealm = "playground";
    public const string DefaultApiUrl = "https://api-test.platformaofd.ru";

    [JsonPropertyName("incomingFetchIntervalMinutes")]
    public int IncomingFetchIntervalMinutes { get; set; } = 5;

    [JsonPropertyName("ofdClientId")]
    public string OfdClientId { get; set; } = "";

    [JsonPropertyName("ofdClientSecret")]
    public string OfdClientSecret { get; set; } = "";

    [JsonPropertyName("ofdKeycloakUrl")]
    public string OfdKeycloakUrl { get; set; } = DefaultKeycloakUrl;

    [JsonPropertyName("ofdRealm")]
    public string OfdRealm { get; set; } = DefaultRealm;

    [JsonPropertyName("ofdApiUrl")]
    public string OfdApiUrl { get; set; } = DefaultApiUrl;

    /// <summary>
    /// Пустой адрес и область возвращаются к тестовому стенду: очищенное поле не должно оставить шлюз без хоста.
    /// </summary>
    public void ApplyDefaults()
    {
        OfdClientId = (OfdClientId ?? "").Trim();
        OfdKeycloakUrl = AddressOr(OfdKeycloakUrl, DefaultKeycloakUrl);
        OfdRealm = string.IsNullOrWhiteSpace(OfdRealm) ? DefaultRealm : OfdRealm.Trim();
        OfdApiUrl = AddressOr(OfdApiUrl, DefaultApiUrl);
    }

    private static string AddressOr(string? value, string fallback)
    {
        var text = (value ?? "").Trim().TrimEnd('/');
        return text.Length == 0 ? fallback : text;
    }

    /// <summary>
    /// Ноль в уже сохранённом файле не должен крутить воркер без паузы.
    /// </summary>
    public TimeSpan IncomingInterval() =>
        TimeSpan.FromMinutes(IncomingFetchIntervalMinutes > 0 ? IncomingFetchIntervalMinutes : 5);

    public Result Check() =>
        IncomingFetchIntervalMinutes > 0
            ? Result.Success()
            : Result.Failure("Как часто загружать ЭТрН: нужно целое число больше нуля");
}
