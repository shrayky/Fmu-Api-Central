using CSharpFunctionalExtensions;
using Domain.Entitys.CrptViolations;

namespace Domain.Entitys.CrptViolations.Interfaces;

public interface ICrptViolationsRepository
{
    Task<Result<List<DateOnly>>> GetDates(string inn, DateOnly from, DateOnly to);
    Task<Result<CrptViolationsDailyEntity>> Get(string id);
    Task<Result> Upsert(CrptViolationsDailyEntity entity);
    Task<Result<List<CrptViolationsDailyEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo);
}
