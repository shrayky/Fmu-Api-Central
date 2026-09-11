using System.Text.Json.Serialization;

namespace Domain.Entitys.CrptViolations.Dto;

public class CrptViolationRow
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("date")]
    public string Date { get; init; } = string.Empty;

    [JsonPropertyName("inn")]
    public string Inn { get; init; } = string.Empty;

    [JsonPropertyName("organizationName")]
    public string OrganizationName { get; init; } = string.Empty;

    [JsonPropertyName("productGroup")]
    public int ProductGroup { get; init; }

    [JsonPropertyName("productGroupName")]
    public string ProductGroupName { get; init; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; init; } = string.Empty;

    [JsonPropertyName("violationResult")]
    public string ViolationResult { get; init; } = string.Empty;

    [JsonPropertyName("violationResultName")]
    public string ViolationResultName { get; init; } = string.Empty;

    [JsonPropertyName("violationNumber")]
    public int ViolationNumber { get; init; }
}
