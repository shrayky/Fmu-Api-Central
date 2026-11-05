using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using EtrnSaby.Xml;
using Microsoft.Extensions.Logging;

namespace EtrnSaby;

public sealed class EtrnSabyGateway : IEtrnOperator
{
    public const int PageSize = 50;

    private readonly SabyJsonRpc _rpc;
    private readonly SabySessionStore _sessions;
    private readonly ISabyAuthenticator _authenticator;
    private readonly ISabyCertificateDirectory _certificates;
    private readonly IEtrnSignaturePort _signer;
    private readonly ILogger<EtrnSabyGateway> _logger;
    private readonly TimeProvider _clock;

    public EtrnSabyGateway(
        SabyJsonRpc rpc,
        SabySessionStore sessions,
        ISabyAuthenticator authenticator,
        ISabyCertificateDirectory certificates,
        IEtrnSignaturePort signer,
        ILogger<EtrnSabyGateway> logger,
        TimeProvider? clock = null)
    {
        _rpc = rpc;
        _sessions = sessions;
        _authenticator = authenticator;
        _certificates = certificates;
        _signer = signer;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public string Type => EtrnOperatorType.Saby;

    public async Task<Result<EtrnFetchResult>> FetchAsync(
        EtrnOperatorContext context,
        CancellationToken cancellationToken)
    {
        var exchanges = new List<EtrnExchange>();
        var page = await CallAsync(
            context,
            EtrnMethods.ChangesList,
            Changes(context),
            "страница изменений",
            exchanges,
            cancellationToken);
        if (!page.Ok)
        {
            return Result.Success(new EtrnFetchResult
            {
                Completed = false,
                Error = page.Error,
                Position = context.Position,
                Exchanges = exchanges
            });
        }

        var events = ReadEvents(page.Result);
        if (events.Count == 0)
        {
            return Result.Success(new EtrnFetchResult
            {
                Completed = true,
                Position = context.Position,
                Exchanges = exchanges
            });
        }

        var documents = new List<EtrnFetchedDocument>();
        var closed = new List<string>();
        var processed = 0;
        foreach (var @event in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!@event.IsAcceptance)
            {
                closed.Add(@event.DocumentId);
                processed++;
                continue;
            }

            var read = await CallAsync(
                context,
                EtrnMethods.ReadDocument,
                new Dictionary<string, object> { ["ИдентификаторДокумента"] = @event.DocumentId },
                @event.DocumentId,
                exchanges,
                cancellationToken);
            if (!read.Ok)
                return Failed(events, processed, documents, closed, exchanges, read.Error, context.Position);

            if (!TryReadTitle(read.Result, out var titleBytes))
                return Failed(events, processed, documents, closed, exchanges, SenderTitleReader.NotParsed, context.Position, keepPosition: true);

            var title = SenderTitleReader.Read(titleBytes);
            if (title.IsFailure)
                return Failed(events, processed, documents, closed, exchanges, title.Error, context.Position, keepPosition: true);

            documents.Add(title.Value.ToFetched(@event.DocumentId));
            processed++;
        }

        return Result.Success(new EtrnFetchResult
        {
            Completed = true,
            Position = EtrnPollPage.Advance(context.Position, events, processed),
            Documents = documents,
            ClosedDocumentIds = closed,
            Exchanges = exchanges
        });
    }

    public async Task<Result<EtrnExecuteResult>> ExecuteAsync(
        EtrnOperatorContext context,
        EtrnExecuteCommand command,
        CancellationToken cancellationToken)
    {
        var exchanges = new List<EtrnExchange>();
        var document = command.Document;
        var decision = command.Decision;
        var xml = ReceiverTitleWriter.Build(document, decision);

        var enclosure = await CallAsync(
            context,
            EtrnMethods.WriteEnclosure,
            new Dictionary<string, object>
            {
                ["ИдентификаторДокумента"] = document.OperatorDocumentId,
                ["Этап"] = EtrnPollEvent.AcceptanceStage,
                ["Имя"] = ReceiverTitleWriter.DefaultFileName,
                ["Данные"] = Convert.ToBase64String(ReceiverTitleWriter.ToUtf8(xml))
            },
            document.Number,
            exchanges,
            cancellationToken);
        if (!enclosure.Ok)
            return Result.Success(FailedExecute(exchanges, enclosure.Error));

        var certificate = _certificates.Find(context.CertificateNumber);
        if (certificate.IsFailure)
            return Result.Success(FailedExecute(exchanges, certificate.Error));

        var prepared = await CallAsync(
            context,
            EtrnMethods.PrepareAction,
            new Dictionary<string, object>
            {
                ["ИдентификаторДокумента"] = document.OperatorDocumentId,
                ["Этап"] = EtrnPollEvent.AcceptanceStage,
                ["Действие"] = decision.Action,
                ["Отпечаток"] = certificate.Value.Thumbprint
            },
            document.Number,
            exchanges,
            cancellationToken);
        if (!prepared.Ok)
            return Result.Success(FailedExecute(exchanges, prepared.Error));

        var hash = Text(prepared.Result, "Хеш", "Hash", "ХешФайла");
        var link = Text(prepared.Result, "Ссылка", "Link", "Url", "АдресСсылки");
        var signed = await _signer.SignAsync(
            new EtrnSignatureRequest
            {
                CertificateNumber = context.CertificateNumber,
                CertificatePassword = context.CertificatePassword,
                Hash = hash,
                FileUrl = link
            },
            cancellationToken);
        if (signed.IsFailure)
            return Result.Success(FailedExecute(exchanges, signed.Error));

        var executed = await CallAsync(
            context,
            EtrnMethods.ExecuteAction,
            new Dictionary<string, object>
            {
                ["ИдентификаторДокумента"] = document.OperatorDocumentId,
                ["Этап"] = EtrnPollEvent.AcceptanceStage,
                ["Действие"] = decision.Action,
                ["Подпись"] = signed.Value
            },
            document.Number,
            exchanges,
            cancellationToken);
        if (!executed.Ok)
            return Result.Success(FailedExecute(exchanges, executed.Error));

        return Result.Success(new EtrnExecuteResult
        {
            Completed = true,
            Exchanges = exchanges
        });
    }

    public async Task<Result<EtrnStageResult>> ReadStageAsync(
        EtrnOperatorContext context,
        string operatorDocumentId,
        CancellationToken cancellationToken)
    {
        var exchanges = new List<EtrnExchange>();
        var read = await CallAsync(
            context,
            EtrnMethods.ReadDocument,
            new Dictionary<string, object> { ["ИдентификаторДокумента"] = operatorDocumentId },
            operatorDocumentId,
            exchanges,
            cancellationToken);
        if (!read.Ok)
            return Result.Success(new EtrnStageResult
            {
                Completed = false,
                Error = read.Error,
                Exchanges = exchanges
            });

        var stage = TextDeep(read.Result, "Этап", "Stage");
        if (stage.Length == 0)
            return Result.Success(new EtrnStageResult
            {
                Completed = false,
                Error = "Saby не вернула этап документа.",
                Exchanges = exchanges
            });

        return Result.Success(new EtrnStageResult
        {
            Completed = true,
            Stage = stage,
            Exchanges = exchanges
        });
    }

    private Dictionary<string, object> Changes(EtrnOperatorContext context)
    {
        var parameters = new Dictionary<string, object>
        {
            ["Тип"] = "ConsignmentNote",
            ["ИНН"] = context.Inn,
            ["ПолныйСертификатЭП"] = "Нет",
            ["РазмерСтраницы"] = PageSize
        };

        if (context.Position.IsEmpty)
        {
            parameters["ДатаВремяС"] = _clock.GetLocalNow()
                .AddDays(-30)
                .ToString("yyyy-MM-ddTHH:mm:sszzz");
        }
        else
        {
            parameters["ИдентификаторСобытия"] = context.Position.EventId;
            parameters["ИдентификаторДокумента"] = context.Position.DocumentId;
        }

        return parameters;
    }

    private async Task<ServiceOutcome> CallAsync(
        EtrnOperatorContext context,
        string method,
        object parameters,
        string label,
        List<EtrnExchange> exchanges,
        CancellationToken cancellationToken)
    {
        var answer = await SendWithSessionAsync(context, method, parameters, cancellationToken);
        if (answer.RequestBody.Length > 0)
        {
            exchanges.Add(new EtrnExchange
            {
                Method = method,
                Parameters = label,
                RequestBody = answer.RequestBody,
                ResponseBody = answer.RawBody,
                Completed = answer.Ok,
                Error = answer.Ok ? "" : answer.Error
            });
        }

        return answer.Ok
            ? new ServiceOutcome(true, answer.Result, answer.ResultText, "")
            : new ServiceOutcome(false, null, "", answer.Error);
    }

    /// <summary>
    /// Команда, которой Saby ответила про кончившуюся сессию, повторяется один раз после нового входа.
    /// </summary>
    private async Task<SabyRpcAnswer> SendWithSessionAsync(
        EtrnOperatorContext context,
        string method,
        object parameters,
        CancellationToken cancellationToken)
    {
        var key = SabySessionStore.Key(context.OrganizationId, context.CertificateNumber);
        var session = await _sessions.GetAsync(
            key,
            token => _authenticator.LoginAsync(context, token),
            cancellationToken);
        if (session.IsFailure)
            return new SabyRpcAnswer { Error = session.Error };

        var first = await _rpc.SendAsync(
            SabyJsonRpc.ServiceUrl,
            method,
            parameters,
            session.Value,
            SabyUserAgent.Value,
            cancellationToken);
        if (!first.SessionExpired)
            return first;

        _sessions.Invalidate(key, session.Value);
        var fresh = await _sessions.GetAsync(
            key,
            token => _authenticator.LoginAsync(context, token),
            cancellationToken);
        if (fresh.IsFailure)
            return new SabyRpcAnswer { Error = fresh.Error };

        return await _rpc.SendAsync(
            SabyJsonRpc.ServiceUrl,
            method,
            parameters,
            fresh.Value,
            SabyUserAgent.Value,
            cancellationToken);
    }

    private static Result<EtrnFetchResult> Failed(
        IReadOnlyList<EtrnPollEvent> events,
        int processed,
        List<EtrnFetchedDocument> documents,
        List<string> closed,
        List<EtrnExchange> exchanges,
        string error,
        EtrnPollPosition current,
        bool keepPosition = false) =>
        Result.Success(new EtrnFetchResult
        {
            Completed = false,
            Error = error,
            Position = keepPosition || processed >= events.Count
                ? current
                : new EtrnPollPosition(events[processed].EventId, events[processed].DocumentId),
            Documents = documents,
            ClosedDocumentIds = closed,
            Exchanges = exchanges
        });

    private static EtrnExecuteResult FailedExecute(List<EtrnExchange> exchanges, string error) => new()
    {
        Completed = false,
        Error = error,
        Exchanges = exchanges
    };

    private static List<EtrnPollEvent> ReadEvents(JsonElement? result)
    {
        if (result is not JsonElement element)
            return [];

        var array = ArrayOf(element);
        if (array == null)
            return [];

        var events = new List<EtrnPollEvent>();
        foreach (var item in array.Value.EnumerateArray())
        {
            var documentId = Text(item, "ИдентификаторДокумента", "DocumentId", "documentId", "Идентификатор");
            if (documentId.Length == 0)
                documentId = TextDeep(item, "ИдентификаторДокумента", "DocumentId", "documentId");
            if (documentId.Length == 0)
                continue;

            var eventId = Text(item, "ИдентификаторСобытия", "EventId", "eventId");
            if (eventId.Length == 0)
                eventId = TextDeep(item, "ИдентификаторСобытия", "EventId", "eventId");

            var stage = Text(item, "Этап", "Stage");
            if (stage.Length == 0)
                stage = TextDeep(item, "Этап", "Stage");

            events.Add(new EtrnPollEvent(eventId, documentId, stage));
        }

        return events;
    }

    private static JsonElement? ArrayOf(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element;
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in new[] { "Изменения", "Документы", "Список", "Items", "items" })
        {
            var found = ArrayDeep(element, name, depth: 0);
            if (found != null)
                return found;
        }

        return null;
    }

    private static bool TryReadTitle(JsonElement? result, out byte[] bytes)
    {
        bytes = [];
        if (result is not JsonElement element)
            return false;

        if (element.ValueKind == JsonValueKind.String)
            return TryDecode(element.GetString(), out bytes);

        foreach (var name in new[] { "Вложения", "Attachments", "Файлы" })
        {
            var attachments = ArrayDeep(element, name, depth: 0);
            if (attachments is not JsonElement array || array.ValueKind != JsonValueKind.Array)
                continue;

            var candidates = array.EnumerateArray().ToList();
            var preferred = candidates.FirstOrDefault(item =>
                Name(item).Contains("TRNACLGROT", StringComparison.OrdinalIgnoreCase)
                || Name(item).Contains("1110339", StringComparison.OrdinalIgnoreCase));
            if (preferred.ValueKind == JsonValueKind.Undefined)
                preferred = candidates.FirstOrDefault();

            foreach (var candidate in new[] { preferred })
            {
                if (candidate.ValueKind == JsonValueKind.Undefined)
                    continue;

                if (TryDecode(Text(candidate, "Данные", "Data", "Содержимое", "Содержание"), out bytes))
                    return true;
            }
        }

        return false;
    }

    private static JsonElement? ArrayDeep(JsonElement element, string name, int depth)
    {
        if (depth > 3)
            return null;

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(name, out var direct) && direct.ValueKind == JsonValueKind.Array)
                return direct;

            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                    continue;

                var found = ArrayDeep(property.Value, name, depth + 1);
                if (found != null)
                    return found;
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                    continue;

                var found = ArrayDeep(item, name, depth + 1);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private static string Name(JsonElement attachment) =>
        Text(attachment, "Имя", "Name", "Наименование", "ИмяФайла");

    private static bool TryDecode(string? value, out byte[] bytes)
    {
        bytes = [];
        var text = (value ?? "").Trim();
        if (text.Length == 0)
            return false;

        if (text.StartsWith('<'))
        {
            bytes = Encoding.UTF8.GetBytes(text);
            return true;
        }

        try
        {
            bytes = Convert.FromBase64String(text);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Text(JsonElement? element, params string[] names)
    {
        if (element is not JsonElement value || value.ValueKind != JsonValueKind.Object)
            return "";

        foreach (var name in names)
        {
            if (!value.TryGetProperty(name, out var property))
                continue;

            if (property.ValueKind == JsonValueKind.String)
                return property.GetString() ?? "";
            if (property.ValueKind is JsonValueKind.Number)
                return property.GetRawText();
        }

        return "";
    }

    private static string TextDeep(JsonElement? element, params string[] names)
    {
        if (element is not JsonElement value)
            return "";

        if (value.ValueKind == JsonValueKind.Object)
        {
            var direct = Text(value, names);
            if (direct.Length > 0)
                return direct;

            foreach (var property in value.EnumerateObject())
            {
                if (property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                    continue;

                var nested = TextDeep(property.Value, names);
                if (nested.Length > 0)
                    return nested;
            }
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                var nested = TextDeep(item, names);
                if (nested.Length > 0)
                    return nested;
            }
        }

        return "";
    }

    private readonly record struct ServiceOutcome(bool Ok, JsonElement? Result, string ResultText, string Error);
}
