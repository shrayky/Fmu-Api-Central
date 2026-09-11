using System.Globalization;
using CouchDB.Driver.Extensions;
using CSharpFunctionalExtensions;
using Domain.Entitys.CrptViolations;
using Domain.Entitys.CrptViolations.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class CrptViolationsRepository : BaseCouchDbRepository<CrptViolationsDailyEntity>, ICrptViolationsRepository
{
    public CrptViolationsRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().CrptViolations, services)
    {
    }

    /// <summary>
    /// Возвращает даты уже сохранённых документов по ИНН в диапазоне.
    /// </summary>
    public async Task<Result<List<DateOnly>>> GetDates(string inn, DateOnly from, DateOnly to)
    {
        if (!_appState.DbState())
            return Result.Failure<List<DateOnly>>(DatabaseUnavailable);

        try
        {
            var fromTimestamp = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue)).ToUnixTimeSeconds();
            var toTimestamp = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue)).ToUnixTimeSeconds();
            var queryLimit = (await _parameters.Current()).DatabaseConnection.QueryLimit;

            var entities = await _database
                .Where(document => document.Data.Inn == inn
                    && document.Data.Date >= fromTimestamp
                    && document.Data.Date <= toTimestamp)
                .Take(queryLimit)
                .ToListAsync();

            var dates = entities
                .Select(item => DateFromEntity(item.Data))
                .Where(date => date != null)
                .Select(date => date!.Value)
                .Distinct()
                .ToList();

            return Result.Success(dates);
        }
        catch (Exception ex)
        {
            return Result.Failure<List<DateOnly>>($"Ошибка чтения дат отклонений ЧЗ для {inn}: {ex.Message}");
        }
    }

    private static DateOnly? DateFromEntity(CrptViolationsDailyEntity entity)
    {
        var separator = entity.Id.LastIndexOf('_');
        if (separator > 0
            && DateOnly.TryParseExact(
                entity.Id[(separator + 1)..],
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var fromId))
            return fromId;

        if (entity.Date <= 0)
            return null;

        return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(entity.Date).LocalDateTime);
    }

    /// <summary>
    /// Читает документ отклонений по id.
    /// </summary>
    public async Task<Result<CrptViolationsDailyEntity>> Get(string id)
    {
        if (!_appState.DbState())
            return Result.Failure<CrptViolationsDailyEntity>(DatabaseUnavailable);

        try
        {
            var entity = await GetByIdAsync(id);
            return entity is null
                ? Result.Failure<CrptViolationsDailyEntity>($"Не найден документ отклонений ЧЗ {id}")
                : Result.Success(entity);
        }
        catch (Exception ex)
        {
            return Result.Failure<CrptViolationsDailyEntity>($"Ошибка чтения отклонений ЧЗ {id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Создаёт или перезаписывает документ отклонений за день.
    /// </summary>
    public async Task<Result> Upsert(CrptViolationsDailyEntity entity)
    {
        if (!_appState.DbState())
            return Result.Failure(DatabaseUnavailable);

        try
        {
            return await CreateAsync(entity)
                ? Result.Success()
                : Result.Failure($"Не удалось сохранить отклонения ЧЗ {entity.Id}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Ошибка сохранения отклонений ЧЗ {entity.Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Возвращает документы отклонений за указанный период.
    /// </summary>
    public async Task<Result<List<CrptViolationsDailyEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo)
    {
        if (!_appState.DbState())
            return Result.Failure<List<CrptViolationsDailyEntity>>(DatabaseUnavailable);

        try
        {
            var fromTimestamp = new DateTimeOffset(dateFrom.Date).ToUnixTimeSeconds();
            var toTimestamp = new DateTimeOffset(dateTo.Date.AddDays(1).AddSeconds(-1)).ToUnixTimeSeconds();
            var queryLimit = (await _parameters.Current()).DatabaseConnection.QueryLimit;

            var entities = await _database
                .Where(document => document.Data.Date >= fromTimestamp && document.Data.Date <= toTimestamp)
                .OrderByDescending(document => document.Data.Date)
                .Take(queryLimit)
                .ToListAsync();

            return Result.Success(entities.Select(item => item.Data).ToList());
        }
        catch (Exception ex)
        {
            return Result.Failure<List<CrptViolationsDailyEntity>>(
                $"Ошибка получения отклонений ЧЗ: {ex.Message}");
        }
    }
}
