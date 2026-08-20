using CSharpFunctionalExtensions;

namespace Domain.Database.Interfaces;

/// <summary>
/// Выгрузка и загрузка документов всех баз CouchDB через JSON-пакеты.
/// </summary>
public interface IDatabaseDumpService
{
    Task<Result<DatabaseDumpFile>> ExportAsync(CancellationToken cancellationToken);

    Task<Result<DatabaseDumpImportResult>> ImportAsync(Stream zipStream, CancellationToken cancellationToken);
}
