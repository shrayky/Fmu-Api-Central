using Application.Database.Services;
using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Database;
using Domain.Database.Interfaces;
using Domain.TrueApiIntegration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class DatabaseExportImportServiceTests
{
    /// <summary>
    /// Импорт больше 100 МБ отклоняется до записи на диск.
    /// </summary>
    [Fact]
    public async Task Import_отклоняет_файл_больше_100_мб()
    {
        var dump = new FakeDumpService();
        var sut = CreateSut(dump);
        var file = new FakeFormFile(100 * 1024 * 1024 + 1);

        var result = await sut.Import(file, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("100", result.Error);
        Assert.False(dump.ImportCalled);
        Assert.False(file.Copied);
    }

    private static DatabaseExportImportService CreateSut(FakeDumpService dump)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<DatabaseExportImportService>>(NullLogger<DatabaseExportImportService>.Instance);
        services.AddSingleton<IDatabaseDumpService>(dump);
        services.AddSingleton<IApplicationState>(new FakeApplicationState());
        return new DatabaseExportImportService(services.BuildServiceProvider());
    }

    private sealed class FakeDumpService : IDatabaseDumpService
    {
        public bool ImportCalled { get; private set; }

        public Task<Result<DatabaseDumpFile>> ExportAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Result<DatabaseDumpImportResult>> ImportAsync(Stream zipStream, CancellationToken cancellationToken)
        {
            ImportCalled = true;
            return Task.FromResult(Result.Failure<DatabaseDumpImportResult>("не должен вызываться"));
        }
    }

    private sealed class FakeApplicationState : IApplicationState
    {
        public void DbStateUpdate(bool isOnline) { }
        public bool DbState() => true;
        public void UpdateNeedRestart(bool need) { }
        public bool NeedRestart() => false;
        public void UpdateTrueApiToken(string inn, string token, DateTime lifeUntil) { }
        public TrueApiToken TrueApiToken(string inn) => new();
        public IReadOnlyList<TrueApiToken> TrueApiTokens() => [];
        public void MarkGisMtPushPending() { }
        public bool GisMtPushPending() => false;
        public void ClearGisMtPushPending() { }
    }

    private sealed class FakeFormFile(long length) : IFormFile
    {
        public bool Copied { get; private set; }
        public string ContentType => "application/zip";
        public string ContentDisposition => string.Empty;
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length { get; } = length;
        public string Name => "file";
        public string FileName => "dump.zip";

        public void CopyTo(Stream target)
        {
            Copied = true;
        }

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            Copied = true;
            return Task.CompletedTask;
        }

        public Stream OpenReadStream() => Stream.Null;
    }
}
