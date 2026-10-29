using System.Text.Json.Serialization;

namespace Domain.Configuration.Options;

public class VetisSettings
{
    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "";

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 60;

    [JsonPropertyName("resultPollAttempts")]
    public int ResultPollAttempts { get; set; } = 8;

    [JsonPropertyName("resultPollDelaySeconds")]
    public int ResultPollDelaySeconds { get; set; } = 3;
}
