using System.Text.Json.Serialization;

namespace Domain.Dto.Checker;

public sealed class CheckerClientSettings
{
    [JsonPropertyName("serverAddress")]
    public string ServerAddress { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}
