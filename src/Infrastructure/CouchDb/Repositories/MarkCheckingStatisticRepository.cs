using Domain.Entitys.MarksCheckStatistic;
using Domain.Entitys.MarksCheckStatistic.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

public class MarkCheckStatisticsRepository : BaseCouchDbRepository<MarkCheckStatisticsEntity>, IMarksCheckStatisticRepository
{
    public MarkCheckStatisticsRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().MarkCheckStatistics, services)
    {

    }

    public Task<bool> CreateNew(MarkCheckStatisticsEntity instanceInformation)
    {
        throw new NotImplementedException();
    }

    public Task<bool> Delete(string entityId)
    {
        throw new NotImplementedException();
    }
}
