using System.Text.Json.Serialization;

namespace Domain.Entitys.AlertTemplates.Dto;

public record AlertDatasetResult
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("items")]
    public IReadOnlyList<string> Items { get; init; } = [];

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    public static AlertDatasetResult Empty { get; } = new();

    public bool HasContent =>
        !string.IsNullOrWhiteSpace(Message) || Items.Count > 0;
}
