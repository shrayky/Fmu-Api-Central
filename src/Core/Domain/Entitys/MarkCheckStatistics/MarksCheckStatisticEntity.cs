using Domain.Entitys.Interfaces;
using System.Text.Json.Serialization;

namespace Domain.Entitys.MarksCheckStatistic;

public class MarkCheckStatisticsEntity : IHaveStringId
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nodeId")]
    public string NodeId = string.Empty;

    [JsonPropertyName("date")]
    public long Date { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("successfulOnlineChecks")]
    public int SuccessfulOnlineChecks { get; init; }

    [JsonPropertyName("successfulOfflineChecks")]
    public int SuccessfulOfflineChecks { get; init; }

    [JsonPropertyName("successCheckRatePercentage")]
    public double SuccessRatePercentage => Total > 0
        ? Math.Round((double)(SuccessfulOnlineChecks + SuccessfulOfflineChecks) / Total * 100, 2)
        : 0;
}
