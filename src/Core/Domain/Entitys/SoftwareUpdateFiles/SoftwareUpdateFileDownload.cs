namespace Domain.Entitys.SoftwareUpdateFiles;

/// <summary>
/// Поток файла обновления с границами байт для ответа 200/206.
/// </summary>
public sealed class SoftwareUpdateFileDownload
{
    public const string RangeNotSatisfiableCode = "RangeNotSatisfiable";

    public required Stream Content { get; init; }
    public required long TotalLength { get; init; }
    public required long From { get; init; }
    public required long To { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";

    public bool IsPartial => TotalLength > 0 && (From > 0 || To < TotalLength - 1);
}
