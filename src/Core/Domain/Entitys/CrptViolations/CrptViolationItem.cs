using System.Text.Json.Serialization;

namespace Domain.Entitys.CrptViolations;

public class CrptViolationItem
{
    [JsonPropertyName("productGroup")]
    public int ProductGroup { get; set; }

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("violationNumber")]
    public int ViolationNumber { get; set; }

    [JsonPropertyName("violationResult")]
    public string ViolationResult { get; set; } = string.Empty;

    [JsonPropertyName("violationResultName")]
    public string ViolationResultName { get; set; } = string.Empty;
}
