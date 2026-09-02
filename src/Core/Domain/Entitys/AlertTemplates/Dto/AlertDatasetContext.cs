namespace Domain.Entitys.AlertTemplates.Dto;

public record AlertDatasetContext
{
    public DateTimeOffset Now { get; init; }
    public IReadOnlyList<AlertInstanceSnapshot> Instances { get; init; } = [];
    public IReadOnlyList<AlertStatisticSnapshot> Statistics { get; init; } = [];
    public AlertSettingsSnapshot Settings { get; init; } = new();
}
