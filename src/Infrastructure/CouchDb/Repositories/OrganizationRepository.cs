using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class OrganizationRepository : BaseCouchDbRepository<OrganizationEntity>, IOrganizationRepository
{
    private const string LogRepository = "организации";

    public OrganizationRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().Organizations, services)
    {
    }

    public async Task<Result> Create(OrganizationEntity entity)
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

    public async Task<Result> Update(OrganizationEntity entity)
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

    public async Task<Result<OrganizationEntity>> GetById(string id)
    {
        if (!_appState.DbState())
            return Result.Failure<OrganizationEntity>(DatabaseUnavailable);

        try
        {
            var searchResult = await GetByIdAsync(id);
            if (searchResult is null)
                return Result.Failure<OrganizationEntity>($"Не найдена запись {LogRepository} с id {id}");

            return Result.Success(searchResult);
        }
        catch (Exception ex)
        {
            return Result.Failure<OrganizationEntity>($"Не удалось прочитать запись {LogRepository} с {id} в БД {ex.Message}");
        }
    }

    public async Task<Result<OrganizationEntity>> GetByInn(string inn)
    {
        if (!_appState.DbState())
            return Result.Failure<OrganizationEntity>(DatabaseUnavailable);

        try
        {
            var documents = await _database.Where(p => p.Data.Inn == inn).ToListAsync();
            var entity = documents.FirstOrDefault()?.Data;
            if (entity is null)
                return Result.Failure<OrganizationEntity>($"Не найдена организация с ИНН {inn}");

            return Result.Success(entity);
        }
        catch (Exception ex)
        {
            return Result.Failure<OrganizationEntity>($"Не удалось найти организацию с ИНН {inn}: {ex.Message}");
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

    public async Task<PaginatedResponse<OrganizationEntity>> List(int pageNumber, int pageSize)
    {
        if (!_appState.DbState())
        {
            return new PaginatedResponse<OrganizationEntity>
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

            return new PaginatedResponse<OrganizationEntity>
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
            return new PaginatedResponse<OrganizationEntity>
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

    public async Task<List<OrganizationEntity>> All()
    {
        if (!_appState.DbState())
            return [];

        var appConfig = await _parameters.Current();
        var queryLimit = appConfig.DatabaseConnection.QueryLimit;
        var dbDocs = await _database.Take(queryLimit).ToListAsync();

        return dbDocs.Select(doc => doc.Data)
            .OrderBy(organization => organization.Name)
            .ToList();
    }
}
