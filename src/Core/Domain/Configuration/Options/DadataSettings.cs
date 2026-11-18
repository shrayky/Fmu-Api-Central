using System.Text.Json.Serialization;

namespace Domain.Configuration.Options;

/// <summary>
/// Ключи DaData лежат в config.json и показываются на вкладке «Прочие интеграции»:
/// в переносимый файл настроек они не попадают.
/// </summary>
public class DadataSettings
{
    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("secret")]
    public string Secret { get; set; } = "";

    /// <summary>
    /// Подсказки адреса работают с одним API-ключом.
    /// </summary>
    [JsonIgnore]
    public bool SuggestEnabled => Enable && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Стандартизация адреса дополнительно требует секретный ключ.
    /// </summary>
    [JsonIgnore]
    public bool CleanEnabled => SuggestEnabled && !string.IsNullOrWhiteSpace(Secret);
}
