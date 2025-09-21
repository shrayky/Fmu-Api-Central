using System.Text.Json.Serialization;

namespace Domain.Dto.Responces;

public record PaginatedResponse<T>
{
    [JsonPropertyName("content")]
    public required IEnumerable<T> Content { get; init; }
    [JsonPropertyName("totalCount")]
    public required int TotalCount { get; init; }
    [JsonPropertyName("currentPages")]
    public required int CurrentPage { get; init; }
    [JsonPropertyName("pageSize")]
    public required int PageSize { get; init; }
    [JsonPropertyName("totalPages")]
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage => CurrentPage < TotalPages;
    [JsonPropertyName("hasPreviousPage")]
    public bool HasPreviousPage => CurrentPage > 1;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("filter")]
    public object? SearchTerm { get; init; }
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
    [JsonPropertyName("listEnabled")]
    public bool ListEnabled { get; init; } = true;
}

