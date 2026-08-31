using System.Text.Json.Serialization;

namespace Domain.Entitys.SettingsSchema;

public class HttpRequestTimeouts
{
    [JsonPropertyName("cdnRequestTimeout")]
    public int CdnRequestTimeout { get; set; } = 15;

    [JsonPropertyName("checkMarkRequestTimeout")]
    public int CheckMarkRequestTimeout { get; set; } = 2;

    [JsonPropertyName("checkInternetConnectionTimeout")]
    public int CheckInternetConnectionTimeout { get; set; } = 15;

    [JsonPropertyName("syncWithTsPiot")]
    public bool SyncWithTsPiot { get; set; } = true;
}
