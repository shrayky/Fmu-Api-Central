using System.Text.Json.Serialization;

namespace Domain.Configuration.Options;

/// <summary>
/// Настройки обмена с ГИС МТ.
/// </summary>
public class GisMtSettings
{
    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("serviceUrl")]
    public string ServiceUrl { get; set; } = "http://localhost:2577";

    [JsonPropertyName("mtDocumentsPollIntervalMinutes")]
    public int MtDocumentsPollIntervalMinutes { get; set; } = 10;

    [JsonPropertyName("markRetentionDays")]
    public int MarkRetentionDays { get; set; } = 365;

    [JsonPropertyName("documentsSyncDays")]
    public int DocumentsSyncDays { get; set; } = 1;

    [JsonPropertyName("stockLoadEnabled")]
    public bool StockLoadEnabled { get; set; }

    [JsonPropertyName("stockLoadTime")]
    public TimeOnly StockLoadTime { get; set; } = new(3, 0);
}
