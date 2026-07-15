using CSharpFunctionalExtensions;
using CouchDB.Driver.Extensions;
using CouchDB.Driver.Query.Extensions;
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

    /// <summary>
    /// Возвращает записи статистики проверок за указанный период.
    /// </summary>
    public async Task<Result<List<MarkCheckStatisticsEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo)
    {
        if (!_appState.DbState())
            return Result.Failure<List<MarkCheckStatisticsEntity>>(DatabaseUnavailable);

        try
        {
            var fromTimestamp = new DateTimeOffset(dateFrom.Date).ToUnixTimeSeconds();
            var toTimestamp = new DateTimeOffset(dateTo.Date.AddDays(1).AddSeconds(-1)).ToUnixTimeSeconds();
            var queryLimit = (await _parameters.Current()).DatabaseConnection.QueryLimit;

            var entities = await _database
                .Where(p => p.Data.Date >= fromTimestamp && p.Data.Date <= toTimestamp)
                .OrderByDescending(p => p.Data.Date)
                .Take(queryLimit)
                .ToListAsync();

            return Result.Success(entities.Select(r => r.Data).ToList());
        }
        catch (Exception ex)
        {
            return Result.Failure<List<MarkCheckStatisticsEntity>>(
                $"Ошибка при получении статистики проверок: {ex.Message}");
        }
    }
}
