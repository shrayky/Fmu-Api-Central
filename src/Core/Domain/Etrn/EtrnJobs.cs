using System.Globalization;

namespace Domain.Etrn;

public enum EtrnJobPhase
{
    Starting,
    Running,
    Waiting,
    Stopped,
    Failed
}

public sealed record EtrnJobUpdate(
    string Id,
    string Title,
    string CountLabel,
    int Order,
    EtrnJobPhase Phase,
    DateTimeOffset At,
    DateTimeOffset? NextAt,
    int? Count,
    string? Error);

public sealed class EtrnJobStatusLine
{
    public string Id { get; init; } = "";

    public string Title { get; init; } = "";

    public string Text { get; init; } = "";
}

public interface IEtrnJobStatusStore
{
    void Report(EtrnJobUpdate update);

    IReadOnlyList<EtrnJobStatusLine> Current();
}

public static class EtrnJobStatusText
{
    public static string Format(
        EtrnJobPhase phase,
        string? countLabel,
        int? count,
        DateTimeOffset at,
        DateTimeOffset? nextAt,
        string? error)
    {
        var past = count is int done && !string.IsNullOrWhiteSpace(countLabel)
            ? $" За прошлый запуск {countLabel.Trim()} {done}."
            : "";

        return phase switch
        {
            EtrnJobPhase.Starting => nextAt is DateTimeOffset first
                ? $"Запускается. Первый проход {Moment(first)}."
                : "Запускается. Первый проход после старта API.",
            EtrnJobPhase.Running => $"Выполняется с {Moment(at)}.",
            EtrnJobPhase.Waiting => (nextAt is DateTimeOffset next
                ? $"Ожидает до {Moment(next)}.{past}"
                : $"Ожидает.{past}") + ErrorSuffix(error),
            EtrnJobPhase.Stopped => $"Ожидает: подключения ЭТрН выключены.{past}",
            EtrnJobPhase.Failed => Failed(error, nextAt, count, countLabel),
            _ => ""
        };
    }

    private static string Failed(string? error, DateTimeOffset? nextAt, int? count, string? countLabel)
    {
        var reason = string.IsNullOrWhiteSpace(error) ? "сбой задания" : error.Trim();
        var retry = nextAt is DateTimeOffset at ? $" Повтор {Moment(at)}." : "";
        var done = count is int value && !string.IsNullOrWhiteSpace(countLabel)
            ? $" За этот запуск {countLabel.Trim()} {value}."
            : "";
        return $"Ошибка: {reason}.{retry}{done}";
    }

    private static string ErrorSuffix(string? error) =>
        string.IsNullOrWhiteSpace(error) ? "" : $" Ошибка: {error.Trim()}";

    private static string Moment(DateTimeOffset value) =>
        value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
}
