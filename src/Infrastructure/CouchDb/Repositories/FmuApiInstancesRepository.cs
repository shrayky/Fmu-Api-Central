using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Dto;
using Domain.Dto.Interfaces;
using Domain.Dto.Responces;
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
        var createResult = await CreateAsync(instance);

        return createResult ? Result.Success() : Result.Failure($"Не удалось создать или обновить данные инстанса {instance.Id}");
    }

    public async Task<Result<InstanceEntity>> ByToken(string token)
    {
        var entity = await GetByIdAsync(token);

        if (entity == null)
            return Result.Failure<InstanceEntity>($"Не найден fmu-api с токеном {token}");
        
        return Result.Success(entity);
    }

    public async Task<Result<PaginatedResponse<InstanceEntity>>> List(int pageNumber, int pageSize, string filter = "")
    {
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
        var createResult = await CreateAsync(entity);

        if (createResult)
            Result.Success();

        return Result.Failure<bool>("Не удалось создать запись с инстансом в БД");
    }

    public async Task<Result<bool>> DeleteInstance(InstanceEntity entity)
    {
        var deleteResult = await DeleteAsync(entity.Id);
        
        if (deleteResult)
            Result.Success();

        return Result.Failure<bool>("Не удалось создать запись с инстансом в БД");
    }
    
}