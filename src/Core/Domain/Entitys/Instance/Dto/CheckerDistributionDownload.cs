namespace Domain.Entitys.Instance.Dto;

public sealed class CheckerDistributionDownload
{
    public required Stream Content { get; init; }
    public string FileName { get; init; } = "fmu-api-checker.zip";
    public string ContentType { get; init; } = "application/zip";
}
