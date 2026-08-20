using CSharpFunctionalExtensions;
using Domain.Database;
using Microsoft.AspNetCore.Http;

namespace Application.Database.Interfaces;

/// <summary>
/// Сценарий экспорта и импорта данных CouchDB из настроек базы.
/// </summary>
public interface IDatabaseExportImportService
{
    Task<Result<DatabaseDumpFile>> Export(CancellationToken cancellationToken);

    Task<Result<DatabaseDumpImportResult>> Import(IFormFile file, CancellationToken cancellationToken);
}
