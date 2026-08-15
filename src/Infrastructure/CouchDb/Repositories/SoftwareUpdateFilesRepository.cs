using CouchDb.Http;
using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.Interfaces;
using Domain.Entitys.SoftwareUpdateFiles;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace CouchDb.Repositories;

public class SoftwareUpdateFilesRepository : BaseCouchDbRepository<SoftwareUpdateFilesEntity>, ISoftwareUpdatesRepository
{
    public const string AttachmentHttpClientName = "CouchDbAttachment";

    private readonly IHttpClientFactory _httpClientFactory;

    public SoftwareUpdateFilesRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().SoftwareUpdateFiles, services)
    {
        _httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
    }

    public async Task<Result<bool>> AttachFile(string entityId, string filePath, string contentType)
    {
        if (!_appState.DbState())
            return Result.Failure<bool>(DatabaseUnavailable);

        var doc = await _database.FindAsync(entityId);

        if (doc == null)
            return Result.Failure<bool>($"Обновление ПО с id {entityId} не найдено в БД");

        doc.Attachments.AddOrUpdate(filePath, contentType);

        await _database.AddOrUpdateAsync(doc);

        return Result.Success(true);
    }
    
    public async Task<Result<SoftwareUpdateFilesEntity>> ById(string entityId)
    {
        if (!_appState.DbState())
            return Result.Failure<SoftwareUpdateFilesEntity>(DatabaseUnavailable);

        var existEntity = await GetByIdAsync(entityId);

        if (existEntity == null)
            return Result.Failure<SoftwareUpdateFilesEntity>($"Обновление ПО с id {entityId} не найдено в БД");

        return Result.Success(existEntity)!;
    }

    public async Task<Result<SoftwareUpdateFilesEntity>> MaxUpdateEntity(string os, string architecture, int version, int assembly)
    {
        if (!_appState.DbState())
            return Result.Failure<SoftwareUpdateFilesEntity>(DatabaseUnavailable);

        var newerVersions = await _database.Where(p =>
                p.Data.Os == os &&
                p.Data.Architecture == architecture &&
                p.Data.Version > version)
            .ToListAsync();

        var sameVersionAssembly = await _database.Where(p =>
                p.Data.Os == os &&
                p.Data.Architecture == architecture &&
                p.Data.Version == version &&
                p.Data.Assembly > assembly)
            .ToListAsync();

        var candidates = newerVersions.Concat(sameVersionAssembly).ToList();

        if (candidates.Count == 0)
            return Result.Failure<SoftwareUpdateFilesEntity>($"Не найдено обновление для {version}_{assembly}_{os}_{architecture}");

        var maxEntity = candidates
            .OrderByDescending(v => v.Data.Version)
            .ThenByDescending(a => a.Data.Assembly)
            .First();

        return Result.Success(maxEntity.Data);
    }

    public async Task<Result<SoftwareUpdateFilesEntity>> Create(SoftwareUpdateFilesEntity entity)
    {
        if (!_appState.DbState())
            return Result.Failure<SoftwareUpdateFilesEntity>(DatabaseUnavailable);

        var existEntity = await GetByIdAsync(entity.Id);

        if (existEntity != null) 
            return Result.Failure<SoftwareUpdateFilesEntity>($"Обновление ПО с id {{entityId}} уже существует");

        var creationResult = await CreateAsync(entity);

        if (creationResult)
            return Result.Success(entity);

        return Result.Failure<SoftwareUpdateFilesEntity>($"Не удалось добавить обновление ПО с id {entity.Id}!");

    }

    public async Task<Result<bool>> Delete(string entityId)
    {
        if (!_appState.DbState())
            return Result.Failure<bool>(DatabaseUnavailable);

        var doc = await _database.FindAsync(entityId);

        if (doc == null)
            return Result.Failure<bool>($"Обновление ПО с id {{entityId}} не найдено в БД");

        await _database.RemoveAsync(doc);

        return Result.Success(true);
    }

    public async Task<Result<PaginatedResponse<SoftwareUpdateFilesEntity>>> List(int pageNumber = 1, int pageSize = 50)
    {
        var appConfig = await _parameters.Current();
        var queryLimit = appConfig.DatabaseConnection.QueryLimit;
        var skipElements = (pageNumber - 1) * pageSize;

        if (!_appState.DbState())
            return Result.Success(new PaginatedResponse<SoftwareUpdateFilesEntity>()
            {
                ListEnabled = false,
                Description = DatabaseUnavailable,
                Content = [],
                CurrentPage = 1,
                PageSize = pageSize,
                TotalCount = 0
            });

        try
        {
            var documents = await _database.Skip(skipElements)
                .OrderByDescending(p => p.Data.UpdatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            PaginatedResponse<SoftwareUpdateFilesEntity> responce = new()
            {
                Content = documents.Select(document => document.ToDomain()),
                TotalCount = await RecordCount(),
                PageSize = pageSize,
                CurrentPage = pageNumber,
            };

            return Result.Success(responce);
        }
        catch (Exception ex)
        {
            return Result.Failure<PaginatedResponse<SoftwareUpdateFilesEntity>>(ex.Message);
        }
    }

    public async Task<Result<SoftwareUpdateFileDownload>> FmuApiUpdate(string updateId, long? rangeFrom)
    {
        try
        {
            if (!_appState.DbState())
                return Result.Failure<SoftwareUpdateFileDownload>(DatabaseUnavailable);

            var existEntity = await _database.FindAsync(updateId);

            if (existEntity == null)
                return Result.Failure<SoftwareUpdateFileDownload>($"Обновление ПО с id {updateId} не найдено в БД");

            var attachment = existEntity.Attachments.FirstOrDefault();

            if (attachment == null)
                return Result.Failure<SoftwareUpdateFileDownload>($"Нет присоединенного файла обновления с id {updateId}");

            if (attachment.Uri == null)
                return Result.Failure<SoftwareUpdateFileDownload>($"Вложение обновления с id {updateId} ещё не загружено");

            var totalLength = attachment.Length ?? existEntity.Data.FileSize;
            if (rangeFrom.HasValue && totalLength > 0 && rangeFrom.Value >= totalLength)
                return Result.Failure<SoftwareUpdateFileDownload>(
                    $"{SoftwareUpdateFileDownload.RangeNotSatisfiableCode}:{totalLength}");

            var settings = await _parameters.Current();
            var httpClient = _httpClientFactory.CreateClient(AttachmentHttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, attachment.Uri);

            var authToken = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{settings.DatabaseConnection.UserName}:{settings.DatabaseConnection.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken);

            if (rangeFrom is > 0)
                request.Headers.Range = new RangeHeaderValue(rangeFrom.Value, null);

            var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                response.Dispose();
                return Result.Failure<SoftwareUpdateFileDownload>(
                    $"{SoftwareUpdateFileDownload.RangeNotSatisfiableCode}:{totalLength}");
            }

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.Dispose();
                return Result.Failure<SoftwareUpdateFileDownload>(
                    $"CouchDB вернула ошибку {response.StatusCode}: {error}");
            }

            var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var from = 0L;
            var to = totalLength > 0 ? totalLength - 1 : 0;

            if (response.StatusCode == HttpStatusCode.PartialContent
                && response.Content.Headers.ContentRange is { } contentRange)
            {
                from = contentRange.From ?? rangeFrom ?? 0;
                to = contentRange.To ?? to;
                totalLength = contentRange.Length ?? totalLength;
            }

            return Result.Success(new SoftwareUpdateFileDownload
            {
                Content = new HttpResponseOwnedStream(response, contentStream),
                TotalLength = totalLength,
                From = from,
                To = to,
                ContentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                    ? "application/octet-stream"
                    : attachment.ContentType
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<SoftwareUpdateFileDownload>(
                $"Ошибка загрузки файла обновления {updateId}: {ex.Message}");
        }
    }
}