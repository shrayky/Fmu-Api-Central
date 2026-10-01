namespace Domain.Entitys.AlertTemplates.Dto;

public record AlertCryptoProLicenseSnapshot
{
    public bool Permanent { get; init; }

    public string? ExpiresAt { get; init; }

    public static AlertCryptoProLicenseSnapshot None { get; } = new();
}
