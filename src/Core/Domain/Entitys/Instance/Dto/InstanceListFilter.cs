namespace Domain.Entitys.Instance.Dto;

public record InstanceListFilter
{
    /// <summary>
    /// Значение groupId, которое означает отбор инстансов без группы.
    /// </summary>
    public const string WithoutGroupValue = "__without_group__";

    public string Name { get; init; } = string.Empty;
    public string LocalModuleVersion { get; init; } = string.Empty;
    public string TsPiotVersion { get; init; } = string.Empty;
    public DateTime? TsPiotLicense { get; init; }
    public DateTime? UpdatedBefore { get; init; }
    public string GroupId { get; init; } = string.Empty;

    /// <summary>
    /// Оставляет только инстансы без группы.
    /// </summary>
    public bool WithoutGroup { get; init; }
}
