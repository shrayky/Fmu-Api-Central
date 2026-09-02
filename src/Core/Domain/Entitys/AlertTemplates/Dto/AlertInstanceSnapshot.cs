namespace Domain.Entitys.AlertTemplates.Dto;

/// <summary>
/// Без SecretKey.
/// </summary>
public record AlertInstanceSnapshot
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string LastUpdated { get; init; } = string.Empty;
    public double HoursSinceUpdate { get; init; }
    public IReadOnlyList<AlertLocalModuleSnapshot> LocalModules { get; init; } = [];
    public IReadOnlyList<AlertTsPiotSnapshot> TsPiots { get; init; } = [];
}

public record AlertLocalModuleSnapshot
{
    public int Id { get; init; }
    public string Address { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long LastSync { get; init; }
    public string Status { get; init; } = string.Empty;
    public string OperationMode { get; init; } = string.Empty;
}

public record AlertTsPiotSnapshot
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public bool Online { get; init; }
    public string Version { get; init; } = string.Empty;
    public string? LicenseActiveTill { get; init; }
}
