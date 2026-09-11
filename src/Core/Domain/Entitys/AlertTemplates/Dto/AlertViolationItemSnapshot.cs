namespace Domain.Entitys.AlertTemplates.Dto;

public record AlertViolationItemSnapshot
{
    public int ProductGroup { get; init; }
    public string ProductGroupName { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string ViolationResult { get; init; } = string.Empty;
    public string ViolationResultName { get; init; } = string.Empty;
    public int ViolationNumber { get; init; }
}
