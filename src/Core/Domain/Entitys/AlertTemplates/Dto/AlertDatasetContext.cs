namespace Domain.Entitys.AlertTemplates.Dto;

public record AlertDatasetContext
{
    public DateTimeOffset Now { get; init; }
    public IReadOnlyList<AlertInstanceSnapshot> Instances { get; init; } = [];
    public IReadOnlyList<AlertStatisticSnapshot> Statistics { get; init; } = [];
    public IReadOnlyList<AlertViolationDaySnapshot> Violations { get; init; } = [];
    public IReadOnlyList<AlertOrganizationSnapshot> Organizations { get; init; } = [];
    public AlertCryptoProLicenseSnapshot CryptoProLicense { get; init; } = AlertCryptoProLicenseSnapshot.None;
    public AlertSettingsSnapshot Settings { get; init; } = new();
}
