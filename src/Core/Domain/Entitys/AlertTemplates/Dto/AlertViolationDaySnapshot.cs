namespace Domain.Entitys.AlertTemplates.Dto;

public record AlertViolationDaySnapshot
{
    public string Inn { get; init; } = string.Empty;
    public string OrganizationName { get; init; } = string.Empty;
    public long Date { get; init; }
    public string DateIso { get; init; } = string.Empty;
    public string DateYmd { get; init; } = string.Empty;
    public decimal PenaltyAmountRub { get; init; }
    public IReadOnlyList<AlertViolationItemSnapshot> Violations { get; init; } = [];
}
