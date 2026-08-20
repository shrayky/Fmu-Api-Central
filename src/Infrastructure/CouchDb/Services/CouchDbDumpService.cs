using CouchDb.DatabaseScheme;
using CouchDb.Dto.Dump;
using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Database;
using Domain.Database.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Json;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CouchDb.Services;

[AutoRegisterService(ServiceLifetime.Scoped)]
public class CouchDbDumpService : IDatabaseDumpService
{
    public const string HttpClientName = "CouchDbDump";
    private const string ManifestEntryName = "manifest.json";

    private static readonly JsonSerializerOptions CompactJson = CreateCompactJsonOptions();

    private readonly ILogger<CouchDbDumpService> _logger;
    private readonly IParametersService _parameters;
    private readonly IApplicationState _appState;
    private readonly IHttpClientFactory _httpClientFactory;

    public CouchDbDumpService(IServiceProvider services)
    {
        _logger = services.GetRequiredService<ILogger<CouchDbDumpService>>();
        _parameters = services.GetRequiredService<IParametersService>();
        _appState = services.GetRequiredService<IApplicationState>();
        _httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
    }

    public async Task<Result<DatabaseDumpFile>> ExportAsync(CancellationToken cancellationToken)
    {
        string? tempPath = null;

        try
        {
            if (!_appState.DbState())
                return Result.Failure<DatabaseDumpFile>("База данных недоступна");

            var connection = (await _parameters.Current()).DatabaseConnection;
            var batchSize = Math.Max(connection.BulkBatchSize, 1);
            var dumpFolder = Path.Combine(Path.GetTempPath(), "fmu-api-central-dumps");
            Directory.CreateDirectory(dumpFolder);
            tempPath = Path.Combine(dumpFolder, $"{Guid.NewGuid():N}.zip");

            var manifest = new DatabaseDumpManifest
            {
                ExportedAt = DateTime.Now,
                BulkBatchSize = batchSize
            };

            await using (var zipStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var dbName in DatabaseNames.All().Except(DatabaseNames.ExcludedFromExport(), StringComparer.OrdinalIgnoreCase))
                {
                    var entry = await ExportDatabaseAsync(archive, dbName, batchSize, connection.NetAddress, connection.UserName, connection.Password, cancellationToken);
                    manifest.Databases.Add(entry);
                    _logger.LogInformation("Выгружена база {Database}: {Documents} документов в {Packages} пакетах",
                        dbName, entry.DocumentCount, entry.PackageCount);
                }

                await WriteJsonEntryAsync(archive, ManifestEntryName, manifest, JsonSerializeOptionsProvider.Default(), cancellationToken);
            }

            var fileName = $"fmu-api-central-data-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            return Result.Success(new DatabaseDumpFile
            {
                FilePath = tempPath,
                FileName = fileName
            });
        }
        catch (Exception ex)
        {
            if (tempPath != null && File.Exists(tempPath))
                File.Delete(tempPath);

            _logger.LogError(ex, "Ошибка экспорта баз CouchDB");
            return Result.Failure<DatabaseDumpFile>($"Ошибка экспорта: {ex.Message}");
        }
    }

    public async Task<Result<DatabaseDumpImportResult>> ImportAsync(Stream zipStream, CancellationToken cancellationToken)
    {
        try
        {
            if (!_appState.DbState())
                return Result.Failure<DatabaseDumpImportResult>("База данных недоступна");

            if (!zipStream.CanSeek)
                return Result.Failure<DatabaseDumpImportResult>("Поток архива должен поддерживать позиционирование");

            var connection = (await _parameters.Current()).DatabaseConnection;
            var allowedDatabases = DatabaseNames.All()
                .Except(DatabaseNames.ExcludedFromExport(), StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
            var manifestEntry = archive.GetEntry(ManifestEntryName);
            if (manifestEntry == null)
                return Result.Failure<DatabaseDumpImportResult>("В архиве нет manifest.json");

            await using var manifestStream = manifestEntry.Open();
            var manifest = await JsonSerializer.DeserializeAsync<DatabaseDumpManifest>(manifestStream, JsonSerializeOptionsProvider.Default(), cancellationToken);
            if (manifest == null)
                return Result.Failure<DatabaseDumpImportResult>("Не удалось прочитать манифест архива");

            if (manifest.FormatVersion != DatabaseDumpManifest.CurrentFormatVersion)
                return Result.Failure<DatabaseDumpImportResult>($"Неподдерживаемая версия формата архива: {manifest.FormatVersion}");

            var databases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var packages = 0;
            var documents = 0;

            foreach (var entry in archive.Entries)
            {
                var entryName = entry.FullName.Replace('\\', '/');
                if (string.Equals(entryName, ManifestEntryName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (entry.Length == 0 || entryName.EndsWith('/'))
                    continue;

                if (!entryName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                await using var entryStream = entry.Open();
                using var packageDocument = await JsonDocument.ParseAsync(entryStream, cancellationToken: cancellationToken);

                if (ShouldSkipPackage(packageDocument.RootElement, allowedDatabases, out var skippedDatabase))
                {
                    _logger.LogInformation("Пропущен пакет базы {Database}: она не входит в экспорт данных", skippedDatabase);
                    continue;
                }

                var importResult = await ImportPackageAsync(
                    packageDocument.RootElement,
                    allowedDatabases,
                    connection.NetAddress,
                    connection.UserName,
                    connection.Password,
                    cancellationToken);

                if (importResult.IsFailure)
                    return Result.Failure<DatabaseDumpImportResult>(importResult.Error);

                databases.Add(importResult.Value.Database);
                packages++;
                documents += importResult.Value.DocumentCount;
            }

            _logger.LogInformation("Импорт завершён: баз {Databases}, пакетов {Packages}, документов {Documents}",
                databases.Count, packages, documents);

            return Result.Success(new DatabaseDumpImportResult
            {
                Databases = databases.Count,
                Packages = packages,
                Documents = documents
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка импорта баз CouchDB");
            return Result.Failure<DatabaseDumpImportResult>($"Ошибка импорта: {ex.Message}");
        }
    }

    private async Task<DatabaseDumpManifestEntry> ExportDatabaseAsync(
        ZipArchive archive,
        string dbName,
        int batchSize,
        string netAddress,
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var documentCount = 0;
        var packageIndex = 0;
        string? startKey = null;

        while (true)
        {
            var page = await ReadAllDocsPageAsync(dbName, batchSize, startKey, netAddress, userName, password, cancellationToken);
            if (page.Rows.Count == 0)
                break;

            var documents = new List<JsonObject>();
            foreach (var row in page.Rows)
            {
                if (row.Document == null)
                    continue;

                documents.Add(row.Document);
            }

            if (documents.Count > 0)
            {
                packageIndex++;
                documentCount += documents.Count;
                var package = new DatabaseDumpPackage
                {
                    Database = dbName,
                    PackageIndex = packageIndex,
                    Documents = documents
                };

                await WriteJsonEntryAsync(archive, $"{dbName}/{packageIndex:D4}.json", package, CompactJson, cancellationToken);
            }

            if (page.Rows.Count < batchSize)
                break;

            startKey = page.LastKey;
            if (string.IsNullOrEmpty(startKey))
                break;
        }

        return new DatabaseDumpManifestEntry
        {
            Name = dbName,
            DocumentCount = documentCount,
            PackageCount = packageIndex
        };
    }

    private async Task<AllDocsPage> ReadAllDocsPageAsync(
        string dbName,
        int batchSize,
        string? startKey,
        string netAddress,
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var query = $"include_docs=true&limit={batchSize}";
        if (!string.IsNullOrEmpty(startKey))
            query += $"&startkey={Uri.EscapeDataString($"\"{startKey}\"")}&skip=1";

        var url = BuildUrl(netAddress, $"{dbName}/_all_docs?{query}");
        using var request = CreateAuthorizedRequest(HttpMethod.Get, url, userName, password);
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"CouchDB {dbName} _all_docs: {(int)response.StatusCode} {body}");

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("rows", out var rowsElement) || rowsElement.ValueKind != JsonValueKind.Array)
            return new AllDocsPage();

        var page = new AllDocsPage();
        foreach (var row in rowsElement.EnumerateArray())
        {
            if (row.TryGetProperty("key", out var keyElement) && keyElement.ValueKind == JsonValueKind.String)
                page.LastKey = keyElement.GetString();

            page.Rows.Add(new AllDocsRow
            {
                Document = TryExtractExportDocument(row)
            });
        }

        return page;
    }

    private async Task<Result<PackageImportCounts>> ImportPackageAsync(
        JsonElement packageRoot,
        HashSet<string> allowedDatabases,
        string netAddress,
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        if (!packageRoot.TryGetProperty("database", out var databaseElement) || databaseElement.ValueKind != JsonValueKind.String)
            return Result.Failure<PackageImportCounts>("В пакете нет имени базы");

        var dbName = databaseElement.GetString() ?? string.Empty;
        if (!allowedDatabases.Contains(dbName))
            return Result.Failure<PackageImportCounts>($"База {dbName} не входит в список баз приложения");

        if (!packageRoot.TryGetProperty("documents", out var documentsElement) || documentsElement.ValueKind != JsonValueKind.Array)
            return Result.Failure<PackageImportCounts>($"В пакете базы {dbName} нет массива documents");

        var documents = new List<JsonObject>();
        var ids = new List<string>();
        foreach (var documentElement in documentsElement.EnumerateArray())
        {
            var node = JsonNode.Parse(documentElement.GetRawText()) as JsonObject;
            if (node == null)
                continue;

            var id = node["_id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id) || id.StartsWith("_design/", StringComparison.Ordinal))
                continue;

            documents.Add(node);
            ids.Add(id);
        }

        if (documents.Count == 0)
            return Result.Success(new PackageImportCounts(dbName, 0));

        var revisions = await ReadExistingRevisionsAsync(dbName, ids, netAddress, userName, password, cancellationToken);
        foreach (var document in documents)
        {
            var id = document["_id"]!.GetValue<string>();
            if (revisions.TryGetValue(id, out var rev))
                document["_rev"] = rev;
            else
                document.Remove("_rev");
        }

        var payload = new JsonObject
        {
            ["docs"] = new JsonArray(documents.Select(d => d.DeepClone()).ToArray())
        };

        var url = BuildUrl(netAddress, $"{dbName}/_bulk_docs");
        using var request = CreateAuthorizedRequest(HttpMethod.Post, url, userName, password);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return Result.Failure<PackageImportCounts>($"CouchDB {dbName} _bulk_docs: {(int)response.StatusCode} {body}");

        await Task.Delay(100, cancellationToken);
        return Result.Success(new PackageImportCounts(dbName, documents.Count));
    }

    private async Task<Dictionary<string, string>> ReadExistingRevisionsAsync(
        string dbName,
        List<string> ids,
        string netAddress,
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { keys = ids }, CompactJson);
        var url = BuildUrl(netAddress, $"{dbName}/_all_docs");
        using var request = CreateAuthorizedRequest(HttpMethod.Post, url, userName, password);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"CouchDB {dbName} поиск ревизий: {(int)response.StatusCode} {body}");

        var revisions = new Dictionary<string, string>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("rows", out var rowsElement) || rowsElement.ValueKind != JsonValueKind.Array)
            return revisions;

        foreach (var row in rowsElement.EnumerateArray())
        {
            if (row.TryGetProperty("error", out _))
                continue;

            if (!row.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
                continue;

            if (!row.TryGetProperty("value", out var valueElement) || valueElement.ValueKind != JsonValueKind.Object)
                continue;

            if (valueElement.TryGetProperty("deleted", out var deletedElement) && deletedElement.ValueKind == JsonValueKind.True)
                continue;

            if (!valueElement.TryGetProperty("rev", out var revElement) || revElement.ValueKind != JsonValueKind.String)
                continue;

            var id = idElement.GetString();
            var rev = revElement.GetString();
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(rev))
                revisions[id] = rev;
        }

        return revisions;
    }

    private static bool ShouldSkipPackage(JsonElement packageRoot, HashSet<string> allowedDatabases, out string skippedDatabase)
    {
        skippedDatabase = string.Empty;
        if (!packageRoot.TryGetProperty("database", out var databaseElement) || databaseElement.ValueKind != JsonValueKind.String)
            return false;

        var dbName = databaseElement.GetString() ?? string.Empty;
        if (allowedDatabases.Contains(dbName))
            return false;

        skippedDatabase = dbName;
        return true;
    }

    private static JsonObject? TryExtractExportDocument(JsonElement row)
    {
        if (!row.TryGetProperty("doc", out var doc) || doc.ValueKind != JsonValueKind.Object)
            return null;

        if (!doc.TryGetProperty("_id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
            return null;

        var id = idElement.GetString();
        if (string.IsNullOrEmpty(id) || id.StartsWith("_design/", StringComparison.Ordinal))
            return null;

        if (doc.TryGetProperty("_deleted", out var deletedElement) && deletedElement.ValueKind == JsonValueKind.True)
            return null;

        var result = new JsonObject();
        foreach (var property in doc.EnumerateObject())
        {
            if (property.Name is "_rev" or "_attachments" or "_revisions")
                continue;

            result[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        }

        return result;
    }

    private static async Task WriteJsonEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        T value,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, value, options, cancellationToken);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string userName, string password)
    {
        var request = new HttpRequestMessage(method, url);
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        return request;
    }

    private static string BuildUrl(string netAddress, string relativePath)
    {
        return $"{netAddress.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    private static JsonSerializerOptions CreateCompactJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializeOptionsProvider.Default())
        {
            WriteIndented = false
        };
        return options;
    }

    private sealed class AllDocsPage
    {
        public List<AllDocsRow> Rows { get; } = [];
        public string? LastKey { get; set; }
    }

    private sealed class AllDocsRow
    {
        public JsonObject? Document { get; init; }
    }

    private sealed record PackageImportCounts(string Database, int DocumentCount);
}
