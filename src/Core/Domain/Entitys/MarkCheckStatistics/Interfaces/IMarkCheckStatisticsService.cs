using Domain.Dto.Responces;
using Domain.Entitys.MarkCheckStatistics.Dto;

namespace Domain.Entitys.MarkCheckStatistics.Interfaces;

public interface IMarkCheckStatisticsService
{
    Task<PaginatedResponse<MarkCheckStatisticsPeriodRow>> List(
        int pageNumber,
        int pageSize,
        MarkCheckStatisticsListFilter filter);
}
