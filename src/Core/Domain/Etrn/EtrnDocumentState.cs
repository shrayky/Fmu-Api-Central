using System.Globalization;
using CSharpFunctionalExtensions;

namespace Domain.Etrn;

public static class EtrnDocumentState
{
    public const int ErrorLimit = 300;

    private static readonly TimeSpan SendingLimit = TimeSpan.FromMinutes(10);

    public const string AlreadyRunning = "Приёмка уже выполняется";
    public const string AlreadyClosed = "Документ уже закрыт";

    public static string StageUnavailable(string stage) => $"Сейчас этап «{stage}», приёмка недоступна";

    public static string ForeignNote(string box) => $"Титул адресован {box}, не идентификатору Saby";

    public static bool IsForeignNote(string? error) => ForeignBox(error).Length > 0;

    public static string ForeignBox(string? error)
    {
        var text = (error ?? "").Trim();
        const string prefix = "Титул адресован ";
        const string suffix = ", не идентификатору Saby";
        if (!text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(suffix, StringComparison.Ordinal))
            return "";

        return text[prefix.Length..^suffix.Length];
    }

    public static bool IsStageNote(string? error) =>
        StageOf(error).Length > 0;

    /// <summary>
    /// Список показывает только подпись статуса. Заметка этапа иначе выглядит как «Ждёт приёмки».
    /// </summary>
    public static string StageOf(string? error)
    {
        var text = (error ?? "").Trim();
        const string prefix = "Сейчас этап «";
        const string suffix = "», приёмка недоступна";
        if (!text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(suffix, StringComparison.Ordinal))
            return "";

        return text[prefix.Length..^suffix.Length];
    }

    public static Result StartSending(EtrnIncomingDocumentEntity document, DateTimeOffset now)
    {
        if (document.Status == EtrnDocumentStatus.Sending)
            return Result.Failure(AlreadyRunning);
        if (EtrnDocumentStatus.IsFinal(document.Status))
            return Result.Failure(AlreadyClosed);
        if (!EtrnDocumentStatus.CanAccept(document.Status))
            return Result.Failure(AlreadyClosed);

        document.Status = EtrnDocumentStatus.Sending;
        document.SendingAt = now;
        document.SendingAtUnix = now.ToUnixTimeSeconds();
        document.LastError = "";
        return Result.Success();
    }

    public static Result StartCorrection(EtrnIncomingDocumentEntity document, DateTimeOffset now)
    {
        if (document.Status == EtrnDocumentStatus.Sending)
            return Result.Failure(AlreadyRunning);
        if (!document.CanCorrect)
            return Result.Failure(AlreadyClosed);

        document.Status = EtrnDocumentStatus.Sending;
        document.SendingAt = now;
        document.SendingAtUnix = now.ToUnixTimeSeconds();
        document.LastError = "";
        return Result.Success();
    }

    public static Result Succeed(EtrnIncomingDocumentEntity document, EtrnDecision decision, DateTimeOffset now)
    {
        if (document.Status != EtrnDocumentStatus.Sending)
            return Result.Failure(AlreadyClosed);

        document.Status = decision.Status;
        document.Action = decision.Action;
        document.GeneralComment = decision.GeneralComment;
        document.SignedAt = Moment(now);
        document.SendingAt = null;
        document.SendingAtUnix = 0;
        document.LastError = "";

        var byIndex = decision.Lines.ToDictionary(line => line.Index);
        foreach (var line in document.Lines)
        {
            if (!byIndex.TryGetValue(line.Index, out var decided))
                continue;

            line.FactPlaces = decided.FactPlaces;
            line.FactWeight = decided.FactWeight;
            line.Comment = decided.Comment;
        }

        return Result.Success();
    }

    public static void Fail(EtrnIncomingDocumentEntity document, string? error) =>
        Restore(document, EtrnDocumentStatus.Waiting, error);

    public static void Restore(EtrnIncomingDocumentEntity document, string status, string? error)
    {
        document.Status = status;
        document.SendingAt = null;
        document.SendingAtUnix = 0;
        document.LastError = Truncate(error);
    }

    public static void Close(EtrnIncomingDocumentEntity document)
    {
        document.Status = EtrnDocumentStatus.Closed;
        document.SendingAt = null;
        document.SendingAtUnix = 0;
        document.LastError = "";
    }

    /// <summary>
    /// Документ в СБИС уже завершён. Текст состояния остаётся: Close его стирает, и в списке не видно, почему приёмка закрыта.
    /// </summary>
    public static void Finish(EtrnIncomingDocumentEntity document, string? reason)
    {
        document.Status = EtrnDocumentStatus.Closed;
        document.SendingAt = null;
        document.SendingAtUnix = 0;
        document.LastError = Truncate(reason);
    }

    /// <summary>
    /// Ровно 10 минут — ещё отправка; старше опрос сверяет этап и снимает зависание.
    /// </summary>
    public static Result Reconcile(EtrnIncomingDocumentEntity document, bool stillAcceptanceStage, DateTimeOffset now)
    {
        if (document.Status != EtrnDocumentStatus.Sending || document.SendingAt is not DateTimeOffset started)
            return Result.Success();

        if (now - started <= SendingLimit)
            return Result.Success();

        document.Status = stillAcceptanceStage ? EtrnDocumentStatus.Waiting : EtrnDocumentStatus.Closed;
        document.SendingAt = null;
        document.SendingAtUnix = 0;
        return Result.Success();
    }

    public static string Truncate(string? error)
    {
        var text = (error ?? "").Trim();
        return text.Length <= ErrorLimit ? text : text[..ErrorLimit];
    }

    private static string Moment(DateTimeOffset now) =>
        now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
