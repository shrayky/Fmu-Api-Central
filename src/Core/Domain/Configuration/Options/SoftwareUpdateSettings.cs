using System.Text.Json.Serialization;

namespace Domain.Configuration.Options;

public class SoftwareUpdateSettings
{
    [JsonPropertyName("exchangeServerAddresses")]
    public string ExchangeServerAddresses { get; set; } = string.Empty;

    [JsonPropertyName("exchangeRequestInterval")]
    public int ExchangeRequestInterval { get; set; } = 60;

    [JsonPropertyName("restrictUpdatesOutsideSchedule")]
    public bool RestrictUpdatesOutsideSchedule { get; set; } = true;

    [JsonPropertyName("schedulerUpdateDownload")]
    public List<ScheduleTimeInterval> SchedulerUpdateDownload { get; set; } = [];

    public bool IsDownloadAllowedNow(TimeOnly? now = null)
    {
        if (!RestrictUpdatesOutsideSchedule)
            return true;

        if (SchedulerUpdateDownload.Count == 0)
            return true;

        var current = now ?? TimeOnly.FromDateTime(DateTime.Now);

        return SchedulerUpdateDownload.Any(interval =>
            current >= interval.BeginTime && current <= interval.EndTime);
    }
}

public record ScheduleTimeInterval
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("beginTime")]
    public TimeOnly BeginTime { get; set; }

    [JsonPropertyName("endTime")]
    public TimeOnly EndTime { get; set; }
}
