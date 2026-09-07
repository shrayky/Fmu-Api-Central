using System.Text.Json.Serialization;

namespace Domain.Entitys.Users.Dto;

public record UserView
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("isLastUser")]
    public bool IsLastUser { get; set; }
}
