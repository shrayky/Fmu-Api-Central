using System.Globalization;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using Domain.Etrn.Xml;
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
    private readonly IHttpClientFactory _files;
    private readonly ILogger<EtrnSabyGateway> _logger;
    private readonly TimeProvider _clock;

    public EtrnSabyGateway(
        SabyJsonRpc rpc,
        SabySessionStore sessions,
        ISabyAuthenticator authenticator,
        ISabyCertificateDirectory certificates,
        IEtrnSignaturePort signer,
        IHttpClientFactory files,
        ILogger<EtrnSabyGateway> logger,
        TimeProvider? clock = null)
    {
        _rpc = rpc;
        _sessions = sessions;
        _authenticator = authenticator;
        _certificates = certificates;
        _signer = signer;
        _files = files;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public string Type => EtrnOperatorType.Saby;

    public async Task<Result<EtrnProbeResult>> ProbeAsync(
        EtrnOperatorContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.CertificateNumber))
        {
            return Result.Success(new EtrnProbeResult
            {
                Ok = false,
                Message = "Не выбран сертификат организации"
            });
        }

        var login = await _authenticator.LoginAsync(context, cancellationToken);
        return Result.Success(new EtrnProbeResult
        {
            Ok = login.IsSuccess,
            Message = login.IsSuccess ? "Saby приняла сертификат" : login.Error
        });
    }

    public async Task<Result<EtrnFetchResult>> FetchAsync(
        EtrnOperatorContext context,
        CancellationToken cancellationToken)
    {
        var exchanges = new List<EtrnExchange>();
        var documents = new List<EtrnFetchedDocument>();
        var closed = new List<string>();
        var position = context.Position;
        string? requested = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cursor = position.EventId + "\n" + position.DocumentId + "\n" + position.EventAt;
            // Та же пара и время: Saby повторила страницу, дальше запросы крутятся на месте.
            if (requested == cursor)
                break;

            requested = cursor;
            var current = WithPosition(context, position);
            var page = await CallAsync(
                current,
                EtrnMethods.ChangesList,
                Changes(current),
                "страница изменений",
                exchanges,
                cancellationToken);
            if (!page.Ok)
            {
                return Result.Success(new EtrnFetchResult
                {
                    Completed = false,
                    Error = page.Error,
                    Position = position,
                    Documents = documents,
                    ClosedDocumentIds = closed,
                    Exchanges = exchanges
                });
            }

            // СписокИзменений кладёт накладные в result.Документ, XML титула в теле нет — только Файл.Ссылка.
            if (page.Result is JsonElement listed
                && listed.ValueKind == JsonValueKind.Object
                && listed.TryGetProperty("Документ", out var documentList)
                && documentList.ValueKind == JsonValueKind.Array)
            {
                var part = await FetchDocumentPageAsync(current, documentList, exchanges, cancellationToken);
                documents.AddRange(part.Value.Documents);
                closed.AddRange(part.Value.ClosedDocumentIds);
                position = part.Value.Position;
                if (!part.Value.Completed || !HasMore(listed))
                {
                    return Result.Success(new EtrnFetchResult
                    {
                        Completed = part.Value.Completed,
                        Error = part.Value.Error,
                        Position = position,
                        Documents = documents,
                        ClosedDocumentIds = closed,
                        Exchanges = exchanges
                    });
                }

                continue;
            }

            return await FetchEventPageAsync(current, page.Result, exchanges, cancellationToken);
        }

        return Result.Success(new EtrnFetchResult
        {
            Completed = true,
            Position = position,
            Documents = documents,
            ClosedDocumentIds = closed,
            Exchanges = exchanges
        });
    }

    private async Task<Result<EtrnFetchResult>> FetchEventPageAsync(
        EtrnOperatorContext context,
        JsonElement? result,
        List<EtrnExchange> exchanges,
        CancellationToken cancellationToken)
    {
        var events = ReadEvents(result);
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
                Document(@event.DocumentId),
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

        // Вложение в завершённый документ Saby принимает, а этап «Приемка груза» после этого уже недоступен.
        var read = await CallAsync(
            context,
            EtrnMethods.ReadDocument,
            Document(document.OperatorDocumentId),
            document.Number,
            exchanges,
            cancellationToken);
        if (!read.Ok)
            return Result.Success(FailedExecute(exchanges, read.Error));

        var picture = DocumentPicture.From(read.Result);
        if (picture.Finished)
        {
            var state = picture.StateName.Length > 0 ? picture.StateName : "Код состояния " + picture.StateCode;
            return Result.Success(FailedExecute(exchanges, state, closeDocument: true));
        }

        if (picture.Stage.Length > 0 && !picture.IsAcceptance)
            return Result.Success(FailedExecute(exchanges, EtrnDocumentState.StageUnavailable(picture.Stage)));

        var xml = ReceiverTitleWriter.Build(document, decision);

        var enclosure = await CallAsync(
            context,
            EtrnMethods.WriteEnclosure,
            Enclosure(
                document.OperatorDocumentId,
                Convert.ToBase64String(ReceiverTitleWriter.ToUtf8(xml))),
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
            Prepare(document.OperatorDocumentId, decision.Action, certificate.Value.Thumbprint),
            document.Number,
            exchanges,
            cancellationToken);
        if (!prepared.Ok)
            return Result.Success(FailedExecute(exchanges, prepared.Error));

        var link = TextDeep(prepared.Result, "Ссылка", "Link", "Url", "АдресСсылки");
        // Подпись хеша не совпадает с файлом, который СБИС уже переписала. В инструкции ЭТрН подписывают вложение по ссылке.
        byte[] content = [];
        var hash = "";
        if (link.Length > 0)
        {
            var file = await DownloadAsync(
                context,
                link,
                "Не удалось скачать файл для подписи",
                "Не удалось скачать файл для подписи",
                cancellationToken);
            if (file.IsFailure)
                return Result.Success(FailedExecute(exchanges, file.Error));

            content = file.Value;
        }
        else
        {
            hash = TextDeep(prepared.Result, "Хеш", "Hash", "ХешФайла");
        }

        var signed = await _signer.SignAsync(
            new EtrnSignatureRequest
            {
                CertificateNumber = context.CertificateNumber,
                CertificatePassword = context.CertificatePassword,
                Hash = hash,
                Content = content
            },
            cancellationToken);
        if (signed.IsFailure)
            return Result.Success(FailedExecute(exchanges, signed.Error));

        var executed = await CallAsync(
            context,
            EtrnMethods.ExecuteAction,
            Execute(
                document.OperatorDocumentId,
                decision.Action,
                certificate.Value.Thumbprint,
                AttachmentId(prepared.Result),
                signed.Value),
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
            Document(operatorDocumentId),
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

        var picture = DocumentPicture.From(read.Result);
        if (picture.Finished)
        {
            return Result.Success(new EtrnStageResult
            {
                Completed = true,
                CloseDocument = true,
                Stage = picture.Stage,
                StateName = picture.StateText,
                Exchanges = exchanges
            });
        }

        if (picture.Stage.Length == 0)
            return Result.Success(new EtrnStageResult
            {
                Completed = false,
                Error = "Saby не вернула этап документа.",
                Exchanges = exchanges
            });

        return Result.Success(new EtrnStageResult
        {
            Completed = true,
            Stage = picture.Stage,
            StillAcceptance = picture.IsAcceptance,
            Exchanges = exchanges
        });
    }

    /// <summary>
    /// Фильтр, организация и навигация — объекты. Плоский ИНН и число страницы дают 500: The value is not an object.
    /// Дата — «ДД.ММ.ГГГГ ЧЧ.ММ.СС»: Saby не разбирает ISO.
    /// </summary>
    private Dictionary<string, object> Changes(EtrnOperatorContext context)
    {
        var filter = new Dictionary<string, object>
        {
            ["Тип"] = "ConsignmentNote",
            ["НашаОрганизация"] = Organization(context.Inn),
            ["ПолныйСертификатЭП"] = "Нет",
            ["Навигация"] = new Dictionary<string, string>
            {
                ["РазмерСтраницы"] = PageSize.ToString()
            }
        };

        if (context.Position.IsEmpty)
        {
            var day = DateOnly.ParseExact(
                context.Connection.ResolvedIncomingFrom(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
            filter["ДатаВремяС"] = day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + " 00.00.00";
        }
        else
        {
            filter["ИдентификаторСобытия"] = context.Position.EventId;
            filter["ИдентификаторДокумента"] = context.Position.DocumentId;
            // Без даты Saby берёт «сейчас минус месяц» — границу ближе события — и снова отдаёт первую страницу.
            if (context.Position.EventAt.Length > 0)
                filter["ДатаВремяС"] = context.Position.EventAt;
        }

        return new Dictionary<string, object> { ["Фильтр"] = filter };
    }

    private static Dictionary<string, object> Organization(string? inn)
    {
        var value = (inn ?? "").Trim();
        var party = value.Length == 12 ? "СвФЛ" : "СвЮЛ";
        return new Dictionary<string, object>
        {
            [party] = new Dictionary<string, string> { ["ИНН"] = value }
        };
    }

    /// <summary>
    /// Этап не передаётся: Saby ищет действие с этим названием и отвечает, что действия нет.
    /// Файл — объект. Строка на его месте даёт 500: The value is not an object.
    /// </summary>
    private static Dictionary<string, object> Enclosure(string documentId, string data) =>
        new()
        {
            ["Документ"] = new Dictionary<string, object>
            {
                ["Идентификатор"] = documentId,
                ["Вложение"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["Файл"] = new Dictionary<string, string>
                        {
                            ["Имя"] = ReceiverTitleWriter.DefaultFileName,
                            ["ДвоичныеДанные"] = data
                        }
                    }
                }
            }
        };

    /// <summary>
    /// Этап, действие и сертификат — объекты. Строка на их месте даёт 500: The value is not an object.
    /// </summary>
    private static Dictionary<string, object> Prepare(string documentId, string action, string thumbprint) =>
        new()
        {
            ["Документ"] = new Dictionary<string, object>
            {
                ["Идентификатор"] = documentId,
                ["Этап"] = new Dictionary<string, object>
                {
                    ["Название"] = EtrnPollEvent.AcceptanceStage,
                    ["Действие"] = Action(action, thumbprint)
                }
            }
        };

    private static Dictionary<string, object> Execute(
        string documentId,
        string action,
        string thumbprint,
        string attachmentId,
        string signature)
    {
        var attachment = new Dictionary<string, object>
        {
            ["Подпись"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["Файл"] = new Dictionary<string, string>
                    {
                        ["Имя"] = ReceiverTitleWriter.DefaultFileName + ".sgn",
                        ["ДвоичныеДанные"] = signature
                    }
                }
            }
        };
        if (attachmentId.Length > 0)
            attachment["Идентификатор"] = attachmentId;

        return new Dictionary<string, object>
        {
            ["Документ"] = new Dictionary<string, object>
            {
                ["Идентификатор"] = documentId,
                ["Этап"] = new Dictionary<string, object>
                {
                    ["Название"] = EtrnPollEvent.AcceptanceStage,
                    ["Действие"] = Action(action, thumbprint),
                    ["Вложение"] = new object[] { attachment }
                }
            }
        };
    }

    private static Dictionary<string, object> Action(string action, string thumbprint) =>
        new()
        {
            ["Название"] = action,
            ["Сертификат"] = new Dictionary<string, string> { ["Отпечаток"] = thumbprint }
        };

    private static string AttachmentId(JsonElement? result)
    {
        if (result is not JsonElement element)
            return "";

        var list = ArrayDeep(element, "Вложение", 0);
        if (list is not JsonElement array)
            return "";

        foreach (var item in array.EnumerateArray())
        {
            var id = Text(item, "Идентификатор");
            if (id.Length > 0)
                return id;
        }

        return "";
    }

    private static Dictionary<string, object> Document(string id) =>
        new()
        {
            ["Документ"] = new Dictionary<string, string> { ["Идентификатор"] = id }
        };

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
                : new EtrnPollPosition(events[processed].EventId, events[processed].DocumentId, events[processed].EventAt),
            Documents = documents,
            ClosedDocumentIds = closed,
            Exchanges = exchanges
        });

    private static EtrnExecuteResult FailedExecute(
        List<EtrnExchange> exchanges,
        string error,
        bool closeDocument = false) => new()
    {
        Completed = false,
        CloseDocument = closeDocument,
        Error = error,
        Exchanges = exchanges
    };

    private readonly record struct DocumentPicture(string StateCode, string StateName, string Stage, bool Open)
    {
        public bool Finished => StateCode is "7" or "9";

        /// <summary>
        /// Название «Приемка груза» без Действие Saby всё равно отдаёт. ПодготовитьДействие тогда отвечает, что этап недоступен.
        /// </summary>
        public bool IsAcceptance =>
            Open && string.Equals(Stage, EtrnPollEvent.AcceptanceStage, StringComparison.OrdinalIgnoreCase);

        public string StateText => StateName.Length > 0 ? StateName : "Код состояния " + StateCode;

        public void Apply(EtrnFetchedDocument document)
        {
            if (Finished)
            {
                document.Status = EtrnDocumentStatus.Closed;
                document.Error = StateText;
                return;
            }

            if (Stage.Length > 0 && !IsAcceptance)
            {
                document.Status = EtrnDocumentStatus.Waiting;
                document.Error = EtrnDocumentState.StageUnavailable(Stage);
            }
        }

        public static DocumentPicture From(JsonElement? result)
        {
            if (result is not JsonElement element || element.ValueKind != JsonValueKind.Object)
                return new DocumentPicture("", "", "", false);

            var document = element;
            if (element.TryGetProperty("Документ", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object)
                document = wrapped;

            var stateCode = "";
            var stateName = "";
            if (document.TryGetProperty("Состояние", out var state) && state.ValueKind == JsonValueKind.Object)
            {
                stateCode = Text(state, "Код").Trim();
                stateName = Text(state, "Название").Trim();
            }

            var stage = StageOf(document);
            return new DocumentPicture(stateCode, stateName, stage.Name, stage.Open);
        }

        private static (string Name, bool Open) StageOf(JsonElement document)
        {
            if (!document.TryGetProperty("Этап", out var stage))
                return ("", false);

            if (stage.ValueKind == JsonValueKind.String)
                return ((stage.GetString() ?? "").Trim(), false);

            if (stage.ValueKind == JsonValueKind.Object)
                return (Text(stage, "Название").Trim(), HasAction(stage));

            if (stage.ValueKind != JsonValueKind.Array)
                return ("", false);

            var named = "";
            foreach (var item in stage.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var name = Text(item, "Название").Trim();
                if (name.Length == 0)
                    continue;

                if (HasAction(item))
                    return (name, true);

                if (named.Length == 0)
                    named = name;
            }

            return (named, false);
        }

        private static bool HasAction(JsonElement stage) =>
            stage.TryGetProperty("Действие", out var action)
            && (action.ValueKind == JsonValueKind.Object
                || (action.ValueKind == JsonValueKind.Array && action.GetArrayLength() > 0));
    };

    private static EtrnOperatorContext WithPosition(EtrnOperatorContext context, EtrnPollPosition position) => new()
    {
        OrganizationId = context.OrganizationId,
        Inn = context.Inn,
        Connection = context.Connection,
        CertificateNumber = context.CertificateNumber,
        CertificatePassword = context.CertificatePassword,
        Position = position
    };

    private static bool HasMore(JsonElement result) =>
        result.TryGetProperty("Навигация", out var navigation)
        && string.Equals(Text(navigation, "ЕстьЕще"), "Да", StringComparison.OrdinalIgnoreCase);

    private async Task<Result<EtrnFetchResult>> FetchDocumentPageAsync(
        EtrnOperatorContext context,
        JsonElement documents,
        List<EtrnExchange> exchanges,
        CancellationToken cancellationToken)
    {
        var rows = new List<(string EventId, string DocumentId, string EventAt, string FileUrl)>();
        foreach (var item in documents.EnumerateArray())
        {
            var documentId = Text(item, "Идентификатор");
            if (documentId.Length == 0)
                continue;

            foreach (var change in ChangesOf(item))
                rows.Add((Text(change, "Идентификатор"), documentId, Text(change, "ДатаВремя"), ShipperFileUrl(change)));
        }

        if (rows.Count == 0)
        {
            return Result.Success(new EtrnFetchResult
            {
                Completed = true,
                Position = context.Position,
                Exchanges = exchanges
            });
        }

        var events = rows.Select(row => new EtrnPollEvent(row.EventId, row.DocumentId, "", row.EventAt)).ToList();
        var fetched = new List<EtrnFetchedDocument>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = rows[index];
            if (row.FileUrl.Length == 0 || !seen.Add(row.DocumentId))
                continue;

            var downloaded = await DownloadTitleAsync(context, row.FileUrl, cancellationToken);
            if (downloaded.IsFailure)
                return Failed(events, index, fetched, [], exchanges, downloaded.Error, context.Position);

            var title = SenderTitleReader.Read(downloaded.Value);
            if (title.IsFailure)
                return Failed(events, index, fetched, [], exchanges, title.Error, context.Position, keepPosition: true);

            // В событии списка нет состояния документа: без чтения завершённая накладная станет «Ждёт приёмки».
            var state = await CallAsync(
                context,
                EtrnMethods.ReadDocument,
                Document(row.DocumentId),
                row.DocumentId,
                exchanges,
                cancellationToken);
            if (!state.Ok)
                return Failed(events, index, fetched, [], exchanges, state.Error, context.Position);

            var fetchedDocument = title.Value.ToFetched(row.DocumentId);
            DocumentPicture.From(state.Result).Apply(fetchedDocument);
            fetched.Add(fetchedDocument);
        }

        return Result.Success(new EtrnFetchResult
        {
            Completed = true,
            Position = EtrnPollPage.Advance(context.Position, events, events.Count),
            Documents = fetched,
            Exchanges = exchanges
        });
    }

    private Task<Result<byte[]>> DownloadTitleAsync(
        EtrnOperatorContext context,
        string url,
        CancellationToken cancellationToken) =>
        DownloadAsync(
            context,
            url,
            "Не удалось скачать титул грузоотправителя",
            SenderTitleReader.NotParsed,
            cancellationToken);

    private async Task<Result<byte[]>> DownloadAsync(
        EtrnOperatorContext context,
        string url,
        string failure,
        string empty,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.GetAsync(
            SabySessionStore.Key(context.OrganizationId, context.CertificateNumber),
            token => _authenticator.LoginAsync(context, token),
            cancellationToken);
        if (session.IsFailure)
            return Result.Failure<byte[]>(session.Error);

        try
        {
            using var http = _files.CreateClient(EtrnSignatureService.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            // disk.sbis.ru без сессии списка отвечает 403.
            request.Headers.TryAddWithoutValidation("X-SBISSessionID", session.Value);
            request.Headers.TryAddWithoutValidation("User-Agent", SabyUserAgent.Value);
            using var response = await http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0)
                return Result.Failure<byte[]>(empty);

            return Result.Success(bytes);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                   && ex is HttpRequestException or TaskCanceledException)
        {
            return Result.Failure<byte[]>($"{failure}: {ex.Message}");
        }
    }

    private static IEnumerable<JsonElement> ChangesOf(JsonElement document)
    {
        if (document.ValueKind == JsonValueKind.Object
            && document.TryGetProperty("Событие", out var changes)
            && changes.ValueKind == JsonValueKind.Array
            && changes.GetArrayLength() > 0)
        {
            foreach (var change in changes.EnumerateArray())
                yield return change;
            yield break;
        }

        yield return document;
    }

    private static string ShipperFileUrl(JsonElement change)
    {
        if (change.ValueKind != JsonValueKind.Object
            || !change.TryGetProperty("Вложение", out var attachments)
            || attachments.ValueKind != JsonValueKind.Array)
            return "";

        foreach (var attachment in attachments.EnumerateArray())
        {
            if (attachment.ValueKind != JsonValueKind.Object
                || !attachment.TryGetProperty("Файл", out var file)
                || file.ValueKind != JsonValueKind.Object)
                continue;

            var link = Text(file, "Ссылка");
            if (link.Length == 0)
                continue;

            var subtype = Text(attachment, "Подтип");
            var name = Text(file, "Имя");
            if (subtype == "1110339" || name.Contains("TRNACLGROT", StringComparison.OrdinalIgnoreCase))
                return link;
        }

        return "";
    }

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
