using System.Text.Json.Serialization;

namespace Domain.Entitys.Instance.Dto;

public record ForceUpdateRequest
{
    [JsonPropertyName("tokens")]
    public List<string> Tokens { get; init; } = [];

    [JsonPropertyName("updateId")]
    public string UpdateId { get; init; } = string.Empty;
}
