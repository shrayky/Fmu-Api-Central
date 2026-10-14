using System.Text.Json.Serialization;

namespace TrueApiIntegration.Models;

public record PermissiveAccessResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;
}