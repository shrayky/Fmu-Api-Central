using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Dto.Interfaces;
using Domain.Dto.Responces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class SoftwareUpdateFilesRepository : BaseCouchDbRepository<SoftwareUpdateFilesEntity>, ISoftwareUpdatesRepository
{
    private const string DatabaseUnavailable = "БД недоступна сейчас";
    
    public SoftwareUpdateFilesRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().SoftwareUpdateFiles, services)
    {
        
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

        var entity = await _database.FirstOrDefaultAsync(p =>
            p.Data.Os == os && p.Data.Architecture == architecture && p.Data.Version >= version &&
            p.Data.Assembly >= assembly);

        if (entity == null)
            return Result.Failure<SoftwareUpdateFilesEntity>($"Не найдено обновение для {version}_{assembly}_{os}_{architecture}");

        return Result.Success(entity.Data);
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

    public async Task<Result<Stream>> FmuApiUpdate(string updateId)
    {
        if (!_appState.DbState())
            return Result.Failure<Stream>(DatabaseUnavailable);
        
        var existEntity = await _database.FindAsync(updateId);
        
        if (existEntity == null)
            return Result.Failure<Stream>($"Обновление ПО с id {updateId} не найдено в БД");

        var attachment = existEntity.Attachments.FirstOrDefault();

        if (attachment == null)
            return Result.Failure<Stream>($"Нет присоединенного файла обновления с id {updateId}");
        
        var responseStream = await _database.DownloadAttachmentAsStreamAsync(attachment);
        
        return Result.Success(responseStream);
    }
}