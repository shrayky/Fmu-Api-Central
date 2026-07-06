namespace Domain.Entitys.MarksCheckStatistic.Interfaces;

public interface IMarksCheckStatisticRepository
{
    Task<bool> CreateNew(MarkCheckStatisticsEntity instanceInformation);
    Task<bool> Delete(string entityId);
}
