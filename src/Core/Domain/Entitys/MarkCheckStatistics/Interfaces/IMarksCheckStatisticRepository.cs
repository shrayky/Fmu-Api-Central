using CSharpFunctionalExtensions;
using Domain.Entitys.MarksCheckStatistic;

namespace Domain.Entitys.MarkCheckStatistics.Interfaces;

public interface IMarksCheckStatisticRepository
{
    Task<Result> CreateNew(MarkCheckStatisticsEntity statisticsEntity);
    Task<Result> AddRange(List<MarkCheckStatisticsEntity> markCheckStatisticsEntities);
    Task<Result> Delete(string entityId);
    Task<Result<List<MarkCheckStatisticsEntity>>> GetByDateRange(DateTime dateFrom, DateTime dateTo);
}
