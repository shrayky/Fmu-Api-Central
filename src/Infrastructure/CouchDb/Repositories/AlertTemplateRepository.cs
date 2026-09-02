using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.AlertTemplates;
using Domain.Entitys.AlertTemplates.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class AlertTemplateRepository : BaseCouchDbRepository<AlertTemplateEntity>, IAlertTemplateRepository
{
    private const string LogRepository = "шаблона оповещения";

    public AlertTemplateRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().AlertTemplates, services)
    {
    }

    public async Task<Result> Create(AlertTemplateEntity entity)
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

    public async Task<Result> Update(AlertTemplateEntity entity)
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

    public async Task<Result<AlertTemplateEntity>> GetById(string id)
    {
        if (!_appState.DbState())
            return Result.Failure<AlertTemplateEntity>(DatabaseUnavailable);

        try
        {
            var searchResult = await GetByIdAsync(id);
            if (searchResult is null)
                return Result.Failure<AlertTemplateEntity>($"Не найдена запись {LogRepository} с id {id}");

            return Result.Success(searchResult);
        }
        catch (Exception ex)
        {
            return Result.Failure<AlertTemplateEntity>($"Не удалось прочитать запись {LogRepository} с {id} в БД {ex.Message}");
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

    public async Task<PaginatedResponse<AlertTemplateEntity>> List(int pageNumber, int pageSize)
    {
        if (!_appState.DbState())
        {
            return new PaginatedResponse<AlertTemplateEntity>
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

            return new PaginatedResponse<AlertTemplateEntity>
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
            return new PaginatedResponse<AlertTemplateEntity>
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

    public async Task<List<AlertTemplateEntity>> All()
    {
        if (!_appState.DbState())
            return [];

        var appConfig = await _parameters.Current();
        var queryLimit = appConfig.DatabaseConnection.QueryLimit;
        var dbDocs = await _database.Take(queryLimit).ToListAsync();

        return dbDocs.Select(doc => doc.Data)
            .OrderBy(template => template.Name)
            .ToList();
    }

    public async Task<List<AlertTemplateEntity>> AllEnabled()
    {
        var all = await All();
        return all.Where(template => template.Enabled).ToList();
    }
}
