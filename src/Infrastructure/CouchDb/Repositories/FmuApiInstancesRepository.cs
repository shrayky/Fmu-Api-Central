using CouchDb.Dto;
using CouchDB.Driver.Extensions;
using CouchDB.Driver.Query.Extensions;
using CSharpFunctionalExtensions;
using System.Text.RegularExpressions;
using Domain.Dto.Responces;
using Domain.Entitys.Instance;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

//[AutoRegisterService(ServiceLifetime.Scoped)]
public class FmuApiInstancesRepository : BaseCouchDbRepository<InstanceEntity>, IInstanceRepository
{
    public FmuApiInstancesRepository(IServiceProvider services) : base(services.GetRequiredService<Context>().FmuApiInstances, services)
    {
    }

    public async Task<Result> Update(InstanceEntity instance)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);
        
        var createResult = await CreateAsync(instance);

        return createResult ? Result.Success() : Result.Failure($"Не удалось создать или обновить данные узла {instance.Id}");
    }

    public async Task<Result<InstanceEntity>> ByToken(string token)
    {
        if (!_appState.DbState())
            return Result.Failure<InstanceEntity>(DatabaseUnavailable);
        
        var entity = await GetByIdAsync(token);

        if (entity == null)
            return Result.Failure<InstanceEntity>($"Не найден fmu-api с токеном {token}");
        
        return Result.Success(entity);
    }

    public async Task<Result<PaginatedResponse<InstanceEntity>>> List(int pageNumber, int pageSize, InstanceListFilter filter)
    {
        if (!_appState.DbState())
        {
            PaginatedResponse<InstanceEntity> emptyResponse = new()
            {
                Description = DatabaseUnavailable,
                ListEnabled = false,
                TotalCount = 1,
                PageSize = pageSize,
                CurrentPage = 1,
                Content = new List<InstanceEntity>()
            };
            
            return Result.Success(emptyResponse);
        }
        
        try
        {
            var query = ApplyListFilter(_database.AsQueryable(), filter);

            var entities = await query
                .OrderByDescending(p => p.Data.UpdatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalCount = HasActiveFilter(filter)
                ? await GetFilteredCountAsync(query)
                : await RecordCount();
            
            var answer = new PaginatedResponse<InstanceEntity>()
            {
                Content = entities.Select(r => r.Data),
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                SearchTerm = filter
            };
            
            return Result.Success(answer);
        }
        catch (Exception ex)
        {
            return Result.Failure<PaginatedResponse<InstanceEntity>>(ex.Message);
        }
    }

    public async Task<Result<bool>> CreateInstance(InstanceEntity entity)
    {
        if (!_appState.DbState())
            return Result.Failure<bool>(DatabaseUnavailable);
        
        var createResult = await CreateAsync(entity);

        if (!createResult)
            Result.Failure<bool>($"Не удалось создать запись узла с {entity.Id} в БД");

        return Result.Success(true);
    }

    public async Task<Result<bool>> DeleteInstance(InstanceEntity entity)
    {
        if (!_appState.DbState())
            return Result.Failure<bool>(DatabaseUnavailable);
        
        var deleteResult = await DeleteAsync(entity.Id);
        
        if (deleteResult)
            return Result.Success(true);

        return Result.Failure<bool>("Не удалось удалить запись узел из БД");
    }

    public async Task<Result<List<InstanceEntity>>> OfflineInstances(DateTime toDate)
    {
        if (!_appState.DbState())
            return Result.Failure<List<InstanceEntity>>(DatabaseUnavailable);

        try
        {
            var offline = await _database.Where(p => p.Data.UpdatedAt < toDate).ToListAsync();

            List<InstanceEntity> answer = [];
            answer.AddRange(offline.Select(node => node.Data));

            return Result.Success(answer);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<InstanceEntity>>(ex.Message);
        }
    }

    public async Task<Result<List<InstanceEntity>>> All()
    {
        if (!_appState.DbState())
            return Result.Failure<List<InstanceEntity>>(DatabaseUnavailable);

        var appConfig = await _parameters.Current();
        var queryLimit = appConfig.DatabaseConnection.QueryLimit;

        try
        {
            var entities = await _database.Take(queryLimit).ToListAsync();
            
            List<InstanceEntity> answer = [];
            answer.AddRange(entities.Select(node => node.Data));

            return Result.Success(answer);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<InstanceEntity>>(ex.Message);
        }
    }

    private static bool HasActiveFilter(InstanceListFilter filter) =>
        !string.IsNullOrEmpty(filter.Name) ||
        !string.IsNullOrEmpty(filter.LocalModuleVersion) ||
        !string.IsNullOrEmpty(filter.TsPiotVersion) ||
        filter.TsPiotLicense.HasValue;

    private static IQueryable<UniversalDocument<InstanceEntity>> ApplyListFilter(
        IQueryable<UniversalDocument<InstanceEntity>> query,
        InstanceListFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.Name))
        {
            var namePattern = $"(?i).*{Regex.Escape(filter.Name)}.*";
            query = query.Where(p => p.Data.Name.IsMatch(namePattern));
        }

        if (!string.IsNullOrEmpty(filter.LocalModuleVersion))
            query = query.Where(p => p.Data.LocalModules.Any(lm => lm.Version == filter.LocalModuleVersion));

        if (!string.IsNullOrEmpty(filter.TsPiotVersion))
            query = query.Where(p => p.Data.TsPiots.Any(t => t.Version == filter.TsPiotVersion));

        if (filter.TsPiotLicense.HasValue)
        {
            var licenseTimestampLimit = GetLicenseTimestampLimit(filter.TsPiotLicense.Value);
            query = query.Where(p => p.Data.TsPiots.Any(t =>
                t.LicenseActiveTillTimeStamp != null &&
                t.LicenseActiveTillTimeStamp <= licenseTimestampLimit));
        }

        return query;
    }

    private static int GetLicenseTimestampLimit(DateTime licenseDate) =>
        (int)new DateTimeOffset(licenseDate.Date.AddDays(1).AddSeconds(-1)).ToUnixTimeSeconds();

    private async Task<int> GetFilteredCountAsync(IQueryable<UniversalDocument<InstanceEntity>> query)
    {
        var queryLimit = (await _parameters.Current()).DatabaseConnection.QueryLimit;
        var filtered = await query.Take(queryLimit).ToListAsync();

        return filtered.Count;
    }
}