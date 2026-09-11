using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Entitys.CrptViolations;
using Domain.Entitys.CrptViolations.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TrueApiIntegration.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class CrptStatisticsClient(
    IHttpClientFactory httpClientFactory,
    ILogger<CrptStatisticsClient> logger) : ICrptStatisticsClient
{
    private const string Url = "https://markirovka.crpt.ru";
    private const string StatisticsPath = "/bff-elk/v1/directory/statistics";
    private const string ViolationsDataSet = "ANALYTICAL_VIOLATIONS_TOTAL_BY_INN";
    private const string PenaltyDataSet = "ANALYTICAL_VIOLATIONS_AUTO_PENALTY";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<CrptStatisticsClient> _logger = logger;

    /// <summary>
    /// Запрашивает накопительные отклонения ЧЗ за месяц (фильтр с 1-го по 1-е).
    /// </summary>
    public Task<Result<string>> FetchViolations(string inn, string token, DateOnly date, CancellationToken cancellationToken)
        => Post(inn, token, CrptViolationsLoadRules.MonthStart(date), ViolationsDataSet, "2", cancellationToken);

    /// <summary>
    /// Запрашивает накопительный автоштраф ЧЗ за месяц (фильтр с 1-го по 1-е).
    /// </summary>
    public Task<Result<string>> FetchPenalty(string inn, string token, DateOnly date, CancellationToken cancellationToken)
        => Post(inn, token, CrptViolationsLoadRules.MonthStart(date), PenaltyDataSet, "1", cancellationToken);

    private async Task<Result<string>> Post(
        string inn,
        string token,
        DateOnly date,
        string dataSet,
        string version,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient("TrueApiIntegration");
            httpClient.BaseAddress = new Uri(Url);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new CrptStatisticsRequest
            {
                DataSet = dataSet,
                Version = version,
                Filters = new CrptStatisticsFilters
                {
                    Inn = inn,
                    ProductGroups = CrptProductGroups.All,
                    DateBegin = date.ToString("yyyy-MM-dd"),
                    DateEnd = date.ToString("yyyy-MM-dd")
                }
            };

            var response = await httpClient.PostAsJsonAsync(StatisticsPath, request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return Result.Failure<string>($"{StatisticsPath} вернул {response.StatusCode}: {body}");

            return Result.Success(body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка запроса статистики ЧЗ {DataSet} для {Inn} за {Date}", dataSet, inn, date);
            return Result.Failure<string>(ex.Message);
        }
    }

    private sealed class CrptStatisticsRequest
    {
        [JsonPropertyName("data_set")]
        public string DataSet { get; set; } = string.Empty;

        [JsonPropertyName("filters")]
        public CrptStatisticsFilters Filters { get; set; } = new();

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    private sealed class CrptStatisticsFilters
    {
        [JsonPropertyName("inn")]
        public string Inn { get; set; } = string.Empty;

        [JsonPropertyName("product_groups")]
        public int[] ProductGroups { get; set; } = [];

        [JsonPropertyName("date_begin")]
        public string DateBegin { get; set; } = string.Empty;

        [JsonPropertyName("date_end")]
        public string DateEnd { get; set; } = string.Empty;
    }
}
