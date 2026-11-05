using System.Globalization;
using CSharpFunctionalExtensions;

namespace Domain.Etrn;

public static class EtrnDocumentState
{
    public const int ErrorLimit = 300;

    private static readonly TimeSpan SendingLimit = TimeSpan.FromMinutes(10);

    public const string AlreadyRunning = "Приёмка уже выполняется";
    public const string AlreadyClosed = "Документ уже закрыт";

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

    public static void Fail(EtrnIncomingDocumentEntity document, string? error)
    {
        document.Status = EtrnDocumentStatus.Waiting;
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
