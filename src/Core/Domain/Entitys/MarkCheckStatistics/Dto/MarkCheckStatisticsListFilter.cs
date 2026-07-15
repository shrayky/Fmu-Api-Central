namespace Domain.Entitys.MarkCheckStatistics.Dto;

public record MarkCheckStatisticsListFilter
{
    public string Name { get; init; } = string.Empty;
    public double? SuccessRateMin { get; init; }
    public double? OfflineRateMin { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
}
