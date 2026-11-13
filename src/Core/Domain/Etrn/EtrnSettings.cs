using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

namespace Domain.Etrn;

public class EtrnSettings
{
    /// <summary>
    /// В новом файле и в файле без поля ЭТрН выключена.
    /// </summary>
    [JsonPropertyName("enable")]
    public bool Enable { get; set; } = false;

    [JsonPropertyName("incomingFetchIntervalMinutes")]
    public int IncomingFetchIntervalMinutes { get; set; } = 5;

    [JsonPropertyName("requestRetentionDays")]
    public int RequestRetentionDays { get; set; } = 1;

    [JsonPropertyName("failedRequestRetentionDays")]
    public int FailedRequestRetentionDays { get; set; } = 7;

    /// <summary>
    /// Ноль в уже сохранённом файле не должен крутить воркер без паузы.
    /// </summary>
    public TimeSpan IncomingInterval() =>
        TimeSpan.FromMinutes(Positive(IncomingFetchIntervalMinutes, 5));

    public TimeSpan CompletedRetention() => TimeSpan.FromDays(Positive(RequestRetentionDays, 1));

    public TimeSpan FailedRetention() => TimeSpan.FromDays(Positive(FailedRequestRetentionDays, 7));

    public Result Check()
    {
        if (IncomingFetchIntervalMinutes <= 0)
            return Result.Failure("Как часто загружать ЭТрН: нужно целое число больше нуля");

        if (RequestRetentionDays <= 0)
            return Result.Failure("Хранить выполненные запросы: нужно целое число больше нуля");

        if (FailedRequestRetentionDays <= 0)
            return Result.Failure("Хранить запросы с ошибкой или без ответа: нужно целое число больше нуля");

        return Result.Success();
    }

    private static int Positive(int value, int fallback) => value > 0 ? value : fallback;
}
