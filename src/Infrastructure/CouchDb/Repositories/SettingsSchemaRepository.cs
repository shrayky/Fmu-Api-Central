using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.SettingsSchema;
using Domain.Entitys.SettingsSchema.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class SettingsSchemaRepository : BaseCouchDbRepository<SettingsSchemaEntity>, ISettingsSchemaRepository
{
    private const string LogRepository = "схемы настроек";

    public SettingsSchemaRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().SettingsSchemas, services)
    {
    }

    public async Task<Result> Create(SettingsSchemaEntity entity)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            var createResult = await CreateAsync(entity);
            return createResult
                ? Result.Success()
                : Result.Failure($"Не удалось создать запись {LogRepository} с {entity.Id} в БД");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Не удалось создать запись {LogRepository} с {entity.Id} в БД {ex.Message}");
        }
    }

    public async Task<Result> Update(SettingsSchemaEntity entity)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            var updateResult = await CreateAsync(entity);
            return updateResult
                ? Result.Success()
                : Result.Failure($"Не удалось создать или обновить запись {LogRepository} с {entity.Id}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Не удалось обновить запись {LogRepository} с {entity.Id} в БД {ex.Message}");
        }
    }

    public async Task<Result<SettingsSchemaEntity>> GetById(string id)
    {
        if (!_appState.DbState())
            return Result.Failure<SettingsSchemaEntity>(DatabaseUnavailable);

        try
        {
            var searchResult = await GetByIdAsync(id);
            if (searchResult is null)
                return Result.Failure<SettingsSchemaEntity>($"Не найдена запись {LogRepository} с id {id}");

            return Result.Success(searchResult);
        }
        catch (Exception ex)
        {
            return Result.Failure<SettingsSchemaEntity>($"Не удалось прочитать запись {LogRepository} с {id} в БД {ex.Message}");
        }
    }

    public async Task<Result> Delete(string id)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            var searchResult = await GetByIdAsync(id);
            if (searchResult is null)
                return Result.Failure($"Не найдена запись {LogRepository} с id {id}");

            var deleteResult = await DeleteAsync(searchResult.Id);
            return deleteResult
                ? Result.Success()
                : Result.Failure($"Не удалось удалить запись {LogRepository} с {id} в БД.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Не удалось удалить запись {LogRepository} с {id} в БД {ex.Message}");
        }
    }

    public async Task<PaginatedResponse<SettingsSchemaEntity>> List(int pageNumber, int pageSize)
    {
        if (!_appState.DbState())
        {
            return new PaginatedResponse<SettingsSchemaEntity>
            {
                Description = DatabaseUnavailable,
                ListEnabled = false,
                TotalCount = 1,
                PageSize = pageSize,
                CurrentPage = 1,
                Content = []
            };
        }

        try
        {
            var query = _database.AsQueryable();
            var entities = await query
                .OrderBy(p => p.Data.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResponse<SettingsSchemaEntity>
            {
                Content = entities.Select(record => record.Data),
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = await RecordCount(),
                SearchTerm = ""
            };
        }
        catch (Exception ex)
        {
            return new PaginatedResponse<SettingsSchemaEntity>
            {
                Description = ex.Message,
                ListEnabled = false,
                TotalCount = 1,
                PageSize = pageSize,
                CurrentPage = 1,
                Content = []
            };
        }
    }

    public async Task<List<SettingsSchemaEntity>> All()
    {
        if (!_appState.DbState())
            return [];

        var appConfig = await _parameters.Current();
        var queryLimit = appConfig.DatabaseConnection.QueryLimit;
        var dbDocs = await _database.Take(queryLimit).ToListAsync();

        return dbDocs.Select(doc => doc.Data)
            .OrderBy(schema => schema.Name)
            .ToList();
    }

    public async Task<List<SettingsSchemaEntity>> ByListId(List<string> ids)
    {
        if (!_appState.DbState() || ids.Count == 0)
            return [];

        try
        {
            return await GetListByIdAsync(ids);
        }
        catch
        {
            return [];
        }
    }
}
