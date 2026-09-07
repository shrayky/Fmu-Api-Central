using System.Text.Json.Serialization;

namespace Domain.Configuration.Options;

public class ServerSettings
{
    [JsonPropertyName("apiIpPort")]
    public int ApiIpPort { get; set; } = 2579;

    [JsonPropertyName("corsOrigins")]
    public List<string> CorsOrigins { get; set; } = [];
}
