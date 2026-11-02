using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

namespace Domain.Configuration.Options;

public class VetisSettings
{
    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "";

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 60;

    [JsonPropertyName("resultPollAttempts")]
    public int ResultPollAttempts { get; set; } = 8;

    [JsonPropertyName("resultPollDelaySeconds")]
    public int ResultPollDelaySeconds { get; set; } = 3;

    [JsonPropertyName("incomingFetchIntervalMinutes")]
    public int IncomingFetchIntervalMinutes { get; set; } = 5;

    [JsonPropertyName("incomingFetchPageSize")]
    public int IncomingFetchPageSize { get; set; } = 100;

    [JsonPropertyName("applicationRetentionDays")]
    public int ApplicationRetentionDays { get; set; } = 1;

    [JsonPropertyName("failedApplicationRetentionDays")]
    public int FailedApplicationRetentionDays { get; set; } = 7;

    [JsonPropertyName("autoQuenchIntervalMinutes")]
    public int AutoQuenchIntervalMinutes { get; set; } = 15;

    [JsonPropertyName("autoQuenchBatchSize")]
    public int AutoQuenchBatchSize { get; set; } = 20;

    [JsonPropertyName("requireVehicleForQuench")]
    public bool RequireVehicleForQuench { get; set; }

    // Страница getVetDocumentChangesList не больше 100 записей.
    private const int MaxPageSize = 100;

    public TimeSpan IncomingInterval() => TimeSpan.FromMinutes(Positive(IncomingFetchIntervalMinutes, 5));

    public int IncomingPageSize() => Math.Min(Positive(IncomingFetchPageSize, MaxPageSize), MaxPageSize);

    public TimeSpan CompletedRetention() => TimeSpan.FromDays(Positive(ApplicationRetentionDays, 1));

    public TimeSpan FailedRetention() => TimeSpan.FromDays(Positive(FailedApplicationRetentionDays, 7));

    /// <summary>
    /// Ноль в уже лежащем файле крутил бы задание без паузы. Запись такое число не принимает.
    /// </summary>
    public TimeSpan AutoQuenchInterval() => TimeSpan.FromMinutes(Positive(AutoQuenchIntervalMinutes, 15));

    public int AutoQuenchBatch() => Positive(AutoQuenchBatchSize, 20);

    public Result CheckAutoQuenchNumbers()
    {
        if (AutoQuenchIntervalMinutes <= 0)
            return Result.Failure("Как часто гасить ВСД: нужно целое число больше нуля.");

        if (AutoQuenchBatchSize <= 0)
            return Result.Failure("Сколько входящих документов гасить за запуск: нужно целое число больше нуля.");

        return Result.Success();
    }

    private static int Positive(int value, int fallback) => value > 0 ? value : fallback;
}
