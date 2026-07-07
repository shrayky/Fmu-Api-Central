using CSharpFunctionalExtensions;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Domain.Entitys.MarksCheckStatistic;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class MarkCheckStatisticsRepository : BaseCouchDbRepository<MarkCheckStatisticsEntity>, IMarksCheckStatisticRepository
{
    public MarkCheckStatisticsRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().MarkCheckStatistics, services)
    {

    }

    public async Task<Result> AddRange(List<MarkCheckStatisticsEntity> markCheckStatisticsEntities)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            return await CreateBulkAsync(markCheckStatisticsEntities)
                ? Result.Success()
                : Result.Failure("Не удалось добавить статистику в БД");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка при добавлении статистики в БД: {ex.Message}");
        }
    }

    public async Task<Result> CreateNew(MarkCheckStatisticsEntity instanceInformation)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            return await CreateAsync(instanceInformation)
                ? Result.Success()
                : Result.Failure("Не удалось добавить статистику в БД");
        }
        catch (Exception ex) {
            return Result.Failure($"Ошибка при добавлении статистики в БД: {ex.Message}");
        }

    }

    public async Task<Result> Delete(string entityId)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            return await DeleteAsync(entityId)
                ? Result.Success()
                : Result.Failure($"Не удалось удалить статистику с Id {entityId} из БД");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка при удалении статистики из БД: {ex.Message}");
        }
    }
}
