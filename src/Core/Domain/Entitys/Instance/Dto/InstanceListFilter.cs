namespace Domain.Entitys.Instance.Dto;

public record InstanceListFilter
{
    public string Name { get; init; } = string.Empty;
    public string LocalModuleVersion { get; init; } = string.Empty;
    public string TsPiotVersion { get; init; } = string.Empty;
    public DateTime? TsPiotLicense { get; init; }
    public DateTime? UpdatedBefore { get; init; }
    public string GroupId { get; init; } = string.Empty;
}