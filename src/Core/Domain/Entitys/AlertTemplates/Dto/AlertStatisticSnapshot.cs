namespace Domain.Entitys.AlertTemplates.Dto;

public record AlertStatisticSnapshot
{
    public string NodeId { get; init; } = string.Empty;
    public string InstanceName { get; init; } = string.Empty;
    public long Date { get; init; }
    public string DateIso { get; init; } = string.Empty;
    public int Total { get; init; }
    public int SuccessfulOnlineChecks { get; init; }
    public int SuccessfulOfflineChecks { get; init; }
    public double SuccessRatePercentage { get; init; }
}
