using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

namespace Domain.Etrn;

public class EtrnSettings
{
    [JsonPropertyName("incomingFetchIntervalMinutes")]
    public int IncomingFetchIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Ноль в уже сохранённом файле не должен крутить воркер без паузы.
    /// </summary>
    public TimeSpan IncomingInterval() =>
        TimeSpan.FromMinutes(IncomingFetchIntervalMinutes > 0 ? IncomingFetchIntervalMinutes : 5);

    public Result Check() =>
        IncomingFetchIntervalMinutes > 0
            ? Result.Success()
            : Result.Failure("Как часто загружать ЭТрН: нужно целое число больше нуля");
}
