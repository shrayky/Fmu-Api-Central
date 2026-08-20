namespace Domain.Database;

/// <summary>
/// Временный zip-архив выгрузки баз CouchDB.
/// </summary>
public sealed class DatabaseDumpFile : IDisposable
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public string ContentType { get; init; } = "application/zip";

    public FileStream OpenRead() =>
        new(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.DeleteOnClose);

    public void Dispose()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }
}
