namespace Domain.Entitys.AlertTemplates.Dto;

/// <summary>
/// Без token и chatId.
/// </summary>
public record AlertSettingsSnapshot
{
    public int OfflineNodeAlertInterval { get; init; }
    public AlertLocalModuleSettingsSnapshot LocalModuleAlerts { get; init; } = new();
    public AlertTsPiotSettingsSnapshot TsPiotAlerts { get; init; } = new();
}

public record AlertLocalModuleSettingsSnapshot
{
    public string VersionAlert { get; init; } = string.Empty;
    public int DaysWithoutSynchronization { get; init; }
}

public record AlertTsPiotSettingsSnapshot
{
    public bool StatusAlertEnabled { get; init; }
    public bool LicenseAlertEnabled { get; init; }
    public int LicenseAlertDays { get; init; }
    public string VersionAlert { get; init; } = string.Empty;
}
