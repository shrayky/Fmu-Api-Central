using CSharpFunctionalExtensions;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class VetisApplicationRepository : BaseCouchDbRepository<VetisApplicationEntity>, IVetisApplicationRepository
{
    public VetisApplicationRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().VetisApplications, services)
    {
    }

    public async Task<Result> Save(VetisApplicationEntity application)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            return await CreateAsync(application)
                ? Result.Success()
                : Result.Failure($"Не удалось сохранить заявку Ветис {application.Id}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка сохранения заявки Ветис {application.Id}: {ex.Message}");
        }
    }

    public async Task<Result<VetisApplicationEntity?>> Find(string id)
    {
        if (!_appState.DbState())
            return Result.Failure<VetisApplicationEntity?>(DatabaseUnavailable);

        try
        {
            return Result.Success(await GetByIdAsync(id));
        }
        catch (Exception ex)
        {
            return Result.Failure<VetisApplicationEntity?>($"Ошибка чтения заявки Ветис {id}: {ex.Message}");
        }
    }

    public async Task<Result<List<VetisApplicationEntity>>> List(VetisApplicationFilter filter, int skip, int limit)
    {
        if (!_appState.DbState())
            return Result.Failure<List<VetisApplicationEntity>>(DatabaseUnavailable);

        return await ExecuteMangoQueryAsync(VetisApplicationQuery.List(filter, skip, limit));
    }

    public async Task<Result<List<VetisApplicationEntity>>> ListExpired(bool completed, long createdBefore, int limit)
    {
        if (!_appState.DbState())
            return Result.Failure<List<VetisApplicationEntity>>(DatabaseUnavailable);

        return await ExecuteMangoQueryAsync(VetisApplicationQuery.Expired(completed, createdBefore, limit));
    }

    public async Task<Result> Delete(IReadOnlyCollection<string> ids)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            foreach (var id in ids)
                await DeleteAsync(id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка удаления заявок Ветис: {ex.Message}");
        }
    }
}
