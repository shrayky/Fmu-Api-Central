using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Dto.Responces;
using Domain.Entitys.Interfaces;
using Domain.Entitys.MarkCheckStatistics.Dto;
using Domain.Entitys.MarkCheckStatistics.Interfaces;
using Domain.Entitys.MarksCheckStatistic;

namespace Application.MarkCheckStatistics.Services;

[AutoRegisterService]
public class MarkCheckStatisticsService : IMarkCheckStatisticsService
{
    private readonly IMarksCheckStatisticRepository _statisticsRepository;
    private readonly IInstanceRepository _instanceRepository;

    public MarkCheckStatisticsService(
        IMarksCheckStatisticRepository statisticsRepository,
        IInstanceRepository instanceRepository)
    {
        _statisticsRepository = statisticsRepository;
        _instanceRepository = instanceRepository;
    }

    /// <summary>
    /// Возвращает агрегированную за период статистику проверок по инстансам.
    /// </summary>
    public async Task<PaginatedResponse<MarkCheckStatisticsPeriodRow>> List(
        int pageNumber,
        int pageSize,
        MarkCheckStatisticsListFilter filter)
    {
        var (dateFrom, dateTo) = ResolveDateRange(filter);

        var statisticsResult = await _statisticsRepository.GetByDateRange(dateFrom, dateTo);
        if (statisticsResult.IsFailure)
        {
            return EmptyResponse(pageSize, statisticsResult.Error);
        }

        var instancesResult = await _instanceRepository.All();
        if (instancesResult.IsFailure)
        {
            return EmptyResponse(pageSize, instancesResult.Error);
        }

        var instanceNames = instancesResult.Value.ToDictionary(i => i.Id, i => i.Name);
        var recordsByInstance = statisticsResult.Value
            .GroupBy(entity => entity.NodeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rows = recordsByInstance.Keys
            .Select(nodeId => BuildPeriodRow(nodeId, instanceNames, recordsByInstance))
            .Where(row => MatchesFilter(row, filter))
            .OrderBy(row => row.InstanceName, StringComparer.Create(new System.Globalization.CultureInfo("ru-RU"), true))
            .ToList();

        var totalCount = rows.Count;
        var page = Math.Max(pageNumber, 1);
        var content = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedResponse<MarkCheckStatisticsPeriodRow>
        {
            Content = content,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            SearchTerm = filter
        };
    }

    private static (DateTime DateFrom, DateTime DateTo) ResolveDateRange(MarkCheckStatisticsListFilter filter)
    {
        var today = DateTime.Today;
        var dateTo = filter.DateTo?.Date ?? today;
        var dateFrom = filter.DateFrom?.Date ?? today;

        if (dateFrom > dateTo)
            (dateFrom, dateTo) = (dateTo, dateFrom);

        return (dateFrom, dateTo);
    }

    private static MarkCheckStatisticsPeriodRow BuildPeriodRow(
        string nodeId,
        Dictionary<string, string> instanceNames,
        Dictionary<string, List<MarkCheckStatisticsEntity>> recordsByInstance)
    {
        instanceNames.TryGetValue(nodeId, out var instanceName);
        var records = recordsByInstance[nodeId];

        var total = records.Sum(record => record.Total);
        var online = records.Sum(record => record.SuccessfulOnlineChecks);
        var offline = records.Sum(record => record.SuccessfulOfflineChecks);
        var successful = online + offline;

        var successRate = total > 0 ? Math.Round((double)successful / total * 100, 2) : 0;
        var offlineRate = total > 0 ? Math.Round((double)offline / total * 100, 2) : 0;

        return new MarkCheckStatisticsPeriodRow
        {
            Id = nodeId,
            InstanceName = instanceName ?? nodeId,
            Total = total,
            SuccessfulOnlineChecks = online,
            SuccessfulOfflineChecks = offline,
            SuccessRatePercentage = successRate,
            OfflineRatePercentage = offlineRate
        };
    }

    private static bool MatchesFilter(MarkCheckStatisticsPeriodRow row, MarkCheckStatisticsListFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Name) &&
            !row.InstanceName.Contains(filter.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.SuccessRateMin.HasValue && row.SuccessRatePercentage < filter.SuccessRateMin.Value)
            return false;

        if (filter.OfflineRateMin.HasValue && row.OfflineRatePercentage < filter.OfflineRateMin.Value)
            return false;

        return true;
    }

    private static PaginatedResponse<MarkCheckStatisticsPeriodRow> EmptyResponse(int pageSize, string error) =>
        new()
        {
            Content = [],
            CurrentPage = 1,
            PageSize = pageSize,
            TotalCount = 0,
            ListEnabled = false,
            Description = error
        };
}
