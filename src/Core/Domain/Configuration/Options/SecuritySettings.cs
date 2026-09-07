using System.Text.Json.Serialization;

namespace Domain.Configuration.Options;

public class SecuritySettings
{
    [JsonPropertyName("passwordConfigured")]
    public bool PasswordConfigured { get; set; }

    [JsonPropertyName("allowLegacyAgentApi")]
    public bool AllowLegacyAgentApi { get; set; } = true;
}
