using System.Text.Json.Serialization;

namespace Domain.GisMt.Dto;

/// <summary>
/// Настройки обмена, которые Central отдаёт GisMt (без адреса сервиса).
/// </summary>
public class GisMtRemoteSettings
{
    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("mtDocumentsPollIntervalMinutes")]
    public int MtDocumentsPollIntervalMinutes { get; set; }

    [JsonPropertyName("markRetentionDays")]
    public int MarkRetentionDays { get; set; }

    [JsonPropertyName("documentsSyncDays")]
    public int DocumentsSyncDays { get; set; }

    [JsonPropertyName("stockLoadEnabled")]
    public bool StockLoadEnabled { get; set; }

    [JsonPropertyName("stockLoadTime")]
    public TimeOnly StockLoadTime { get; set; }
}
