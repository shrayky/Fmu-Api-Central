using Domain.Entitys.Interfaces;
using System.Text.Json.Serialization;

namespace Domain.Entitys.CrptViolations;

public class CrptViolationsDailyEntity : IHaveStringId
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("inn")]
    public string Inn { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public long Date { get; set; }

    [JsonPropertyName("violations")]
    public List<CrptViolationItem> Violations { get; set; } = [];

    [JsonPropertyName("penaltyAmountRub")]
    public decimal PenaltyAmountRub { get; set; }

    [JsonPropertyName("snapshotViolations")]
    public List<CrptViolationItem> SnapshotViolations { get; set; } = [];

    [JsonPropertyName("snapshotPenaltyAmountRub")]
    public decimal SnapshotPenaltyAmountRub { get; set; }
}
