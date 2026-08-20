using Application.Database.Interfaces;
using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.Database;
using Domain.Database.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Database.Services;

[AutoRegisterService(ServiceLifetime.Scoped)]
public class DatabaseExportImportService : IDatabaseExportImportService
{
    private readonly ILogger<DatabaseExportImportService> _logger;
    private readonly IDatabaseDumpService _dumpService;
    private readonly IApplicationState _appState;

    public DatabaseExportImportService(IServiceProvider services)
    {
        _logger = services.GetRequiredService<ILogger<DatabaseExportImportService>>();
        _dumpService = services.GetRequiredService<IDatabaseDumpService>();
        _appState = services.GetRequiredService<IApplicationState>();
    }

    public async Task<Result<DatabaseDumpFile>> Export(CancellationToken cancellationToken)
    {
        try
        {
            if (!_appState.DbState())
                return Result.Failure<DatabaseDumpFile>("База данных недоступна");

            _logger.LogInformation("Запрошен экспорт данных CouchDB");
            return await _dumpService.ExportAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сценария экспорта данных CouchDB");
            return Result.Failure<DatabaseDumpFile>($"Ошибка экспорта: {ex.Message}");
        }
    }

    public async Task<Result<DatabaseDumpImportResult>> Import(IFormFile file, CancellationToken cancellationToken)
    {
        string? tempPath = null;

        try
        {
            if (!_appState.DbState())
                return Result.Failure<DatabaseDumpImportResult>("База данных недоступна");

            if (file.Length == 0)
                return Result.Failure<DatabaseDumpImportResult>("Файл импорта пуст");

            var dumpFolder = Path.Combine(Path.GetTempPath(), "fmu-central-dumps");
            Directory.CreateDirectory(dumpFolder);
            tempPath = Path.Combine(dumpFolder, $"{Guid.NewGuid():N}.zip");

            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(fileStream, cancellationToken);
            }

            await using var zipStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await _dumpService.ImportAsync(zipStream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сценария импорта данных CouchDB");
            return Result.Failure<DatabaseDumpImportResult>($"Ошибка импорта: {ex.Message}");
        }
        finally
        {
            if (tempPath != null && File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
