using System.Globalization;

namespace Domain.Vetis;

public enum VetisJobPhase
{
    Starting,
    Preparing,
    Running,
    Waiting,
    Stopped,
    Failed
}

public sealed record VetisJobUpdate(
    string Id,
    string Title,
    string CountLabel,
    int Order,
    VetisJobPhase Phase,
    DateTimeOffset At,
    DateTimeOffset? NextAt,
    int? Count,
    string? Error);

public sealed class VetisJobStatusLine
{
    public string Id { get; init; } = "";

    public string Title { get; init; } = "";

    public string Text { get; init; } = "";
}

public interface IVetisJobStatusStore
{
    void Report(VetisJobUpdate update);

    IReadOnlyList<VetisJobStatusLine> Current();
}

public static class VetisJobStatusText
{
    public static string Format(
        VetisJobPhase phase,
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
            VetisJobPhase.Starting => nextAt is DateTimeOffset first
                ? $"Запускается. Первый проход {Moment(first)}."
                : "Запускается. Первый проход после старта API.",
            VetisJobPhase.Preparing => "Готовит журнал, загрузка ВСД ещё не начата.",
            VetisJobPhase.Running => $"Выполняется с {Moment(at)}.",
            VetisJobPhase.Waiting => nextAt is DateTimeOffset next
                ? $"Ожидает до {Moment(next)}.{past}"
                : $"Ожидает.{past}",
            VetisJobPhase.Stopped => $"Остановлено: Ветис выключен.{past}",
            VetisJobPhase.Failed => Failed(error, nextAt, count, countLabel),
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

    private static string Moment(DateTimeOffset value) =>
        value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
}
