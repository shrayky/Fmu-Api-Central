using Domain.Attributes;
using Domain.Entitys.CrptViolations;
using Domain.Entitys.CrptViolations.Dto;
using Domain.Entitys.CrptViolations.Interfaces;
using Domain.Entitys.Organization.Interfaces;
using Domain.Entitys.SettingsSchema;

namespace Application.CrptViolations.Services;

[AutoRegisterService]
public class CrptViolationsService(
    ICrptViolationsRepository violationsRepository,
    IOrganizationRepository organizationRepository) : ICrptViolationsService
{
    private readonly ICrptViolationsRepository _violationsRepository = violationsRepository;
    private readonly IOrganizationRepository _organizationRepository = organizationRepository;

    /// <summary>
    /// Собирает строки дашборда и KPI за период с фильтром по организации.
    /// </summary>
    public async Task<CrptViolationsDashboard> List(int pageNumber, int pageSize, CrptViolationsListFilter filter)
    {
        var (dateFrom, dateTo) = ResolveDateRange(filter);
        var statisticsResult = await _violationsRepository.GetByDateRange(dateFrom, dateTo);
        if (statisticsResult.IsFailure)
            return Empty(pageSize, statisticsResult.Error);

        var organizations = await _organizationRepository.All();
        var names = organizations
            .Where(item => !string.IsNullOrWhiteSpace(item.Inn))
            .GroupBy(item => item.Inn.Trim())
            .ToDictionary(group => group.Key, group => group.First().Name);

        var documents = statisticsResult.Value
            .Where(item => MatchesInn(item.Inn, filter.Inn))
            .ToList();

        var rows = new List<CrptViolationRow>();
        foreach (var document in documents.OrderByDescending(item => item.Date).ThenBy(item => item.Inn))
        {
            names.TryGetValue(document.Inn, out var organizationName);
            var dateText = DateTimeOffset.FromUnixTimeSeconds(document.Date).LocalDateTime.ToString("yyyy-MM-dd");
            var index = 0;

            foreach (var violation in document.Violations)
            {
                rows.Add(new CrptViolationRow
                {
                    Id = $"{document.Id}_{index}",
                    Date = dateText,
                    Inn = document.Inn,
                    OrganizationName = organizationName ?? document.Inn,
                    ProductGroup = violation.ProductGroup,
                    ProductGroupName = TrueApiProductGroupCatalog.TitleByCode(violation.ProductGroup),
                    Region = violation.Region,
                    ViolationResult = violation.ViolationResult,
                    ViolationResultName = violation.ViolationResultName,
                    ViolationNumber = violation.ViolationNumber
                });
                index++;
            }
        }

        var page = Math.Max(pageNumber, 1);
        var size = Math.Max(pageSize, 1);

        return new CrptViolationsDashboard
        {
            TotalViolations = documents.SelectMany(item => item.Violations).Sum(item => item.ViolationNumber),
            TotalPenaltyAmountRub = documents.Sum(item => item.PenaltyAmountRub),
            TotalCount = rows.Count,
            CurrentPage = page,
            PageSize = size,
            Content = rows.Skip((page - 1) * size).Take(size).ToList()
        };
    }

    private static bool MatchesInn(string inn, string filterInn)
        => string.IsNullOrWhiteSpace(filterInn)
           || string.Equals(inn, filterInn.Trim(), StringComparison.OrdinalIgnoreCase);

    private static (DateTime DateFrom, DateTime DateTo) ResolveDateRange(CrptViolationsListFilter filter)
    {
        var today = DateTime.Today;
        var dateTo = filter.DateTo?.Date ?? today;
        var dateFrom = filter.DateFrom?.Date ?? today;

        if (dateFrom > dateTo)
            (dateFrom, dateTo) = (dateTo, dateFrom);

        return (dateFrom, dateTo);
    }

    private static CrptViolationsDashboard Empty(int pageSize, string error) => new()
    {
        PageSize = pageSize,
        CurrentPage = 1,
        ListEnabled = false,
        Description = error
    };
}
