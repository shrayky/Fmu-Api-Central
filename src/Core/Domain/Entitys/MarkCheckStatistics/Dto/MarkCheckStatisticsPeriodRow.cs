using System.Text.Json.Serialization;

namespace Domain.Entitys.MarkCheckStatistics.Dto;

public record MarkCheckStatisticsPeriodRow
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("instanceName")]
    public string InstanceName { get; init; } = string.Empty;

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("successfulOnlineChecks")]
    public int SuccessfulOnlineChecks { get; init; }

    [JsonPropertyName("successfulOfflineChecks")]
    public int SuccessfulOfflineChecks { get; init; }

    [JsonPropertyName("successRatePercentage")]
    public double SuccessRatePercentage { get; init; }

    [JsonPropertyName("offlineRatePercentage")]
    public double OfflineRatePercentage { get; init; }
}
