namespace Domain.Entitys.AlertTemplates.Dto;

public record AlertOrganizationSnapshot
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Inn { get; init; } = string.Empty;
    public string CertificateNumber { get; init; } = string.Empty;
    public string? CertificateWorkUntil { get; init; }
}
