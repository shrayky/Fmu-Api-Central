using System.Text.Json;
using System.Text.Json.Serialization;

namespace Domain.Entitys.CrptViolations;

public static class CrptViolationsResponseMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Собирает документ дня из ответов ЧЗ по отклонениям и штрафам.
    /// </summary>
    public static CrptViolationsDailyEntity Map(
        string inn,
        DateOnly date,
        string violationsJson,
        string penaltyJson)
    {
        var violations = Deserialize<ViolationsResponse>(violationsJson);
        var penalty = Deserialize<PenaltyResponse>(penaltyJson);
        var items = new List<CrptViolationItem>();

        foreach (var group in violations?.AnalyticalViolationsTotalByInn ?? [])
        {
            foreach (var violation in group.Violations ?? [])
            {
                items.Add(new CrptViolationItem
                {
                    ProductGroup = group.ProductGroup,
                    Region = violation.Region ?? string.Empty,
                    ViolationNumber = violation.ViolationNumber,
                    ViolationResult = violation.ViolationResult ?? string.Empty,
                    ViolationResultName = violation.ViolationResultName ?? string.Empty
                });
            }
        }

        var amount = penalty?.AnalyticalViolationsAutoPenalty?.Sum(item => item.AmountRub) ?? 0;
        var dateTime = date.ToDateTime(TimeOnly.MinValue);

        var snapshot = items.Select(Clone).ToList();

        return new CrptViolationsDailyEntity
        {
            Id = CrptViolationsLoadRules.DocumentId(inn, date),
            Inn = inn,
            Date = new DateTimeOffset(dateTime).ToUnixTimeSeconds(),
            SnapshotViolations = snapshot,
            SnapshotPenaltyAmountRub = amount,
            Violations = items,
            PenaltyAmountRub = amount
        };
    }

    private static CrptViolationItem Clone(CrptViolationItem item) => new()
    {
        ProductGroup = item.ProductGroup,
        Region = item.Region,
        ViolationNumber = item.ViolationNumber,
        ViolationResult = item.ViolationResult,
        ViolationResultName = item.ViolationResultName
    };

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private sealed class ViolationsResponse
    {
        [JsonPropertyName("analytical_violations_total_by_inn")]
        public List<ProductGroupViolations>? AnalyticalViolationsTotalByInn { get; set; }
    }

    private sealed class ProductGroupViolations
    {
        [JsonPropertyName("product_group")]
        public int ProductGroup { get; set; }

        [JsonPropertyName("violations")]
        public List<ViolationDto>? Violations { get; set; }
    }

    private sealed class ViolationDto
    {
        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("violation_number")]
        public int ViolationNumber { get; set; }

        [JsonPropertyName("violation_result")]
        public string? ViolationResult { get; set; }

        [JsonPropertyName("violation_result_name")]
        public string? ViolationResultName { get; set; }
    }

    private sealed class PenaltyResponse
    {
        [JsonPropertyName("analytical_violations_auto_penalty")]
        public List<PenaltyDto>? AnalyticalViolationsAutoPenalty { get; set; }
    }

    private sealed class PenaltyDto
    {
        [JsonPropertyName("amount_rub")]
        public decimal AmountRub { get; set; }
    }
}
