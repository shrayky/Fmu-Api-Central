using System.Text.Json.Serialization;

namespace Domain.Dto.Responces;

public record PaginatedResponse<T>
{
    public required IEnumerable<T> Content { get; init; }
    public required int TotalCount { get; init; }
    public required int CurrentPage { get; init; }
    public required int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasPreviousPage => CurrentPage > 1;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? SearchTerm { get; init; }
}

