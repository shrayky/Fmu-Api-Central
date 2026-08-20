namespace Application.Configuration.DTO;

/// <summary>
/// JSON-файл выгрузки переносимых настроек.
/// </summary>
public sealed class PortableSettingsFile
{
    public required string FileName { get; init; }
    public required string Json { get; init; }
    public string ContentType { get; init; } = "application/json";
}
