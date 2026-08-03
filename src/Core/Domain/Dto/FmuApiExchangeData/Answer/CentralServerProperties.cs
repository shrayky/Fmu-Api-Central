using System.Text.Json.Serialization;
using Domain.Configuration.Options;

namespace Domain.Dto.FmuApiExchangeData.Answer;

public record CentralServerProperties
{
    [JsonPropertyName("exchangeServerAddresses")]
    public string ExchangeServerAddresses { get; init; } = string.Empty;

    [JsonPropertyName("exchangeRequestInterval")]
    public int ExchangeRequestInterval { get; init; }

    [JsonPropertyName("schedulerUpdateDownload")]
    public List<ScheduleTimeInterval> SchedulerUpdateDownload { get; init; } = [];
}
