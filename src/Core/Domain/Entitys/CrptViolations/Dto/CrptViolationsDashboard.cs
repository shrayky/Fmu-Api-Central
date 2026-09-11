using System.Text.Json.Serialization;

namespace Domain.Entitys.CrptViolations.Dto;

public class CrptViolationsDashboard
{
    [JsonPropertyName("totalViolations")]
    public int TotalViolations { get; init; }

    [JsonPropertyName("totalPenaltyAmountRub")]
    public decimal TotalPenaltyAmountRub { get; init; }

    [JsonPropertyName("content")]
    public IReadOnlyList<CrptViolationRow> Content { get; init; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }

    [JsonPropertyName("currentPage")]
    public int CurrentPage { get; init; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    [JsonPropertyName("totalPages")]
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("listEnabled")]
    public bool ListEnabled { get; init; } = true;
}
