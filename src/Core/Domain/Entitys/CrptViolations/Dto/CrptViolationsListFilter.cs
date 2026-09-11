namespace Domain.Entitys.CrptViolations.Dto;

public record CrptViolationsListFilter
{
    public string Inn { get; init; } = string.Empty;
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
}
