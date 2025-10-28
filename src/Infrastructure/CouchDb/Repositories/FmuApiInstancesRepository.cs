using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys;
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

    public async Task<Result<PaginatedResponse<InstanceEntity>>> List(int pageNumber, int pageSize, string filter = "")
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
            var query = _database.AsQueryable();
            
            if (!string.IsNullOrEmpty(filter))
                query = query.Where(p => p.Data.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
            
            var entities = await query
                .OrderByDescending(p => p.Data.UpdatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            var answer = new PaginatedResponse<InstanceEntity>()
            {
                Content = entities.Select(r => r.Data),
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = string.IsNullOrEmpty(filter) ? await RecordCount() : query.Count(), 
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

        try
        {
            var entities = await _database.ToListAsync();
            
            List<InstanceEntity> answer = [];
            answer.AddRange(entities.Select(node => node.Data));

            return Result.Success(answer);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<InstanceEntity>>(ex.Message);
        }
    }
}