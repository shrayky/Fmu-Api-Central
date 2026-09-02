using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class InstanceGroupsRepository : BaseCouchDbRepository<InstanceGroupEntity>, IInstanceGroupRepository
{
    private const string LogRepository = "группы инстансов";

    public InstanceGroupsRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().InstanceGroups, services)
    {
    }

    public async Task<Result> Create(InstanceGroupEntity entity)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            var createResult = await CreateAsync(entity);
            if (!createResult)
                return Result.Failure($"Не удалось создать запись {LogRepository} с {entity.Id} в БД");

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Не удалось создать запись {LogRepository} с {entity.Id} в БД {ex.Message}");
        }
    }

    public async Task<Result> Update(InstanceGroupEntity entity)
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

    public async Task<Result<InstanceGroupEntity>> GetById(string id)
    {
        if (!_appState.DbState())
            return Result.Failure<InstanceGroupEntity>(DatabaseUnavailable);

        try
        {
            var searchResult = await GetByIdAsync(id);
            if (searchResult is null)
                return Result.Failure<InstanceGroupEntity>($"Не найдена запись {LogRepository} с id {id}");

            return Result.Success(searchResult);
        }
        catch (Exception ex)
        {
            return Result.Failure<InstanceGroupEntity>($"Не удалось прочитать запись {LogRepository} с {id} в БД {ex.Message}");
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

    public async Task<PaginatedResponse<InstanceGroupEntity>> List(int pageNumber, int pageSize)
    {
        if (!_appState.DbState())
        {
            return new PaginatedResponse<InstanceGroupEntity>
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

            return new PaginatedResponse<InstanceGroupEntity>
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
            return new PaginatedResponse<InstanceGroupEntity>
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

    public async Task<List<InstanceGroupEntity>> All()
    {
        if (!_appState.DbState())
            return [];

        var appConfig = await _parameters.Current();
        var queryLimit = appConfig.DatabaseConnection.QueryLimit;
        var dbDocs = await _database.Take(queryLimit).ToListAsync();

        return dbDocs.Select(doc => doc.Data)
            .OrderBy(group => group.Name)
            .ToList();
    }

    public async Task<List<InstanceGroupEntity>> ByListId(List<string> ids)
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

    public async Task<Result> ClearSettingsSchemaLink(string settingsSchemaId)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            var groups = await _database.Where(p => p.Data.SettingsSchemaId == settingsSchemaId).ToListAsync();
            if (groups.Count == 0)
                return Result.Success();

            foreach (var document in groups)
                document.Data.SettingsSchemaId = string.Empty;

            await SaveExistingDocumentsAsync(groups);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
