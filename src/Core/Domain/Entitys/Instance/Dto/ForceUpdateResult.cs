using System.Text.Json.Serialization;

namespace Domain.Entitys.Instance.Dto;

public record ForceUpdateResult
{
    [JsonPropertyName("assigned")]
    public int Assigned { get; init; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}
