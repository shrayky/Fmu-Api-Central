using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CertificateSigning;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using Domain.Etrn.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtrnOfd;

public sealed class OfdEtrnGateway : IEtrnOperator
{
    public const string HttpClientName = "EtrnOfd";
    public const string OnTheWay = "CARGO_ON_WAY";

    private const string ShipperKnd = "1110339";
    private const string ConsigneeKnd = "1110341";
    private const string ConsigneeType = "1110341v5.01";
    private const int PageSize = 30;
    private const int PageCap = 100;
    private const int ConsigneeListReads = 5;

    private readonly IHttpClientFactory _http;
    private readonly OfdTokenClient _tokens;
    private readonly IEtrnSignaturePort _signer;
    private readonly ILogger<OfdEtrnGateway> _logger;
    private readonly Func<string, Result<EtrnSignerName>>? _names;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<CancellationToken, Task> _pause;

    [ActivatorUtilitiesConstructor]
    public OfdEtrnGateway(
        IHttpClientFactory http,
        OfdTokenClient tokens,
        IEtrnSignaturePort signer,
        ILogger<OfdEtrnGateway> logger,
        IOrganizationCertificateDirectory certificates)
        : this(
            http,
            tokens,
            signer,
            () => DateTimeOffset.Now,
            logger: logger,
            names: serial => ReadSigner(certificates, serial))
    {
    }

    public OfdEtrnGateway(
        IHttpClientFactory http,
        OfdTokenClient tokens,
        IEtrnSignaturePort signer,
        Func<DateTimeOffset> clock,
        Func<CancellationToken, Task>? pause = null,
        ILogger<OfdEtrnGateway>? logger = null,
        Func<string, Result<EtrnSignerName>>? names = null)
    {
        _http = http;
        _tokens = tokens;
        _signer = signer;
        _logger = logger ?? NullLogger<OfdEtrnGateway>.Instance;
        _names = names;
        _now = clock;
        _pause = pause ?? (cancellationToken => Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken));
    }

    public string Type => EtrnOperatorType.Ofd;

    public async Task<Result<EtrnProbeResult>> ProbeAsync(
        EtrnOperatorContext context,
        CancellationToken cancellationToken)
    {
        var exchanges = new List<EtrnExchange>();
        try
        {
            var ready = Ready(context);
            if (ready.IsFailure)
                return Result.Success(ProbeStopped(ready.Error, exchanges));

            var token = await _tokens.AccessTokenAsync(context, fresh: true, cancellationToken);
            if (token.IsFailure)
                return Result.Success(ProbeStopped(token.Error, exchanges));

            var subscriber = await SubscriberIdAsync(context, token.Value.Token, exchanges, cancellationToken);
            if (subscriber.IsFailure)
                return Result.Success(ProbeStopped(subscriber.Error, exchanges));

            var listUrl = ListUrl(context.Connection.ResolvedApiUrl(), subscriber.Value, null);
            var list = await CallAsync(
                HttpMethod.Get,
                listUrl,
                token.Value.Token,
                null,
                null,
                null,
                EtrnMethods.Probe,
                subscriber.Value,
                TokenNote(token.Value.ExpiresIn),
                exchanges,
                cancellationToken);
            if (!list.Ok)
                return Result.Success(ProbeStopped(list.Error, exchanges));
            if (!TryCards(list.Text, out _))
                return Result.Success(ProbeStopped("Платформа ЭТрН вернула не список ЭТрН", exchanges));

            return Result.Success(new EtrnProbeResult
            {
                Ok = true,
                Message = "Платформа ЭТрН приняла подключение",
                Exchanges = exchanges
            });
        }
        catch (Exception ex) when (IsTransport(ex, cancellationToken))
        {
            return Result.Success(ProbeStopped(ex.Message, exchanges));
        }
    }

    public async Task<Result<EtrnFetchResult>> FetchAsync(
        EtrnOperatorContext context,
        CancellationToken cancellationToken)
    {
        var exchanges = new List<EtrnExchange>();
        try
        {
            var ready = Ready(context);
            if (ready.IsFailure)
                return Result.Success(Stopped(context.Position, exchanges, ready.Error));

            var token = await _tokens.AccessTokenAsync(context, cancellationToken);
            if (token.IsFailure)
                return Result.Success(Stopped(context.Position, exchanges, token.Error));

            var api = context.Connection.ResolvedApiUrl();
            var subscriber = await SubscriberIdAsync(context, token.Value.Token, exchanges, cancellationToken);
            if (subscriber.IsFailure)
                return Result.Success(Stopped(context.Position, exchanges, subscriber.Error));

            var documents = new List<EtrnFetchedDocument>();
            var closed = new List<string>();
            string? fromId = null;
            HashSet<string>? previousIds = null;
            for (var pageNumber = 1; ; pageNumber++)
            {
                var listUrl = ListUrl(api, subscriber.Value, fromId);
                var list = await CallAsync(
                    HttpMethod.Get,
                    listUrl,
                    token.Value.Token,
                    null,
                    null,
                    null,
                    EtrnMethods.OfdList,
                    subscriber.Value,
                    listUrl,
                    exchanges,
                    cancellationToken);
                if (!list.Ok)
                    return Result.Success(Stopped(context.Position, exchanges, list.Error));
                if (!TryCards(list.Text, out var cards))
                    return Result.Success(Stopped(context.Position, exchanges, "Платформа ЭТрН вернула не список ЭТрН"));

                var ids = new HashSet<string>(
                    cards.Select(card => card.Id).Where(id => id.Length > 0),
                    StringComparer.Ordinal);
                var repeated = previousIds != null && ids.SetEquals(previousIds);
                var cursor = MinId(cards);
                var backedUp = fromId == null
                    || (long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var minimum)
                        && long.TryParse(fromId, NumberStyles.None, CultureInfo.InvariantCulture, out var current)
                        && minimum < current);

                // Повтор страницы и страница, которая не сдвинула id назад, уже не продолжение списка.
                var process = !repeated && (cards.Count < PageSize || backedUp);
                if (process)
                {
                    foreach (var card in cards)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!card.OnTheWay)
                        {
                            if (card.Id.Length > 0)
                                closed.Add(card.Id);
                            continue;
                        }

                        var title = await ReadShipperAsync(api, token.Value.Token, card, exchanges, cancellationToken);
                        if (title.IsFailure)
                        {
                            return Result.Success(new EtrnFetchResult
                            {
                                Completed = false,
                                Error = title.Error,
                                Position = context.Position,
                                Documents = documents,
                                ClosedDocumentIds = closed,
                                Exchanges = exchanges
                            });
                        }

                        documents.Add(title.Value);
                    }
                }

                // Страницы идут назад по минимальному id. Конец — короткая страница, пустой ответ,
                // повтор набора id или id, который не стал меньше курсора.
                if (pageNumber > PageCap)
                {
                    return Result.Success(new EtrnFetchResult
                    {
                        Completed = false,
                        Error = "Платформа ЭТрН не отдала конец списка",
                        Position = context.Position,
                        Documents = documents,
                        ClosedDocumentIds = closed,
                        Exchanges = exchanges
                    });
                }

                if (cards.Count < PageSize || !process || cursor.Length == 0)
                    break;

                previousIds = ids;
                fromId = cursor;
            }

            return Result.Success(new EtrnFetchResult
            {
                Completed = true,
                Position = context.Position,
                Documents = documents,
                ClosedDocumentIds = closed,
                Exchanges = exchanges
            });
        }
        catch (Exception ex) when (IsTransport(ex, cancellationToken))
        {
            return Result.Success(Stopped(context.Position, exchanges, ex.Message));
        }
    }

    public async Task<Result<EtrnExecuteResult>> ExecuteAsync(
        EtrnOperatorContext context,
        EtrnExecuteCommand command,
        CancellationToken cancellationToken)
    {
        var exchanges = new List<EtrnExchange>();
        try
        {
            var ready = Ready(context);
            if (ready.IsFailure)
                return Result.Success(new EtrnExecuteResult { Error = ready.Error, Exchanges = exchanges });

            var person = (_names ?? (_ => Result.Failure<EtrnSignerName>(CertificateSubjectName.Missing)))
                .Invoke(context.CertificateNumber);
            if (person.IsFailure)
                return Result.Success(new EtrnExecuteResult { Error = person.Error, Exchanges = exchanges });

            var token = await _tokens.AccessTokenAsync(context, cancellationToken);
            if (token.IsFailure)
                return Result.Success(new EtrnExecuteResult { Error = token.Error, Exchanges = exchanges });

            var xml = ConsigneeTitleWriter.Build(
                command.Document,
                command.Decision,
                _now(),
                person.Value,
                command.Correction);
            var bytes = ConsigneeTitleWriter.ToWindows1251(xml);
            var id = (command.Document.OperatorDocumentId ?? "").Trim();
            var api = context.Connection.ResolvedApiUrl();

            // Старый титул грузополучателя уже мог лежать на накладной. Запоминаем его id,
            // чтобы после PUT подписать только новый документ.
            var known = await ConsigneeIdsAsync(api, token.Value.Token, id, exchanges, cancellationToken);
            if (known.IsFailure)
                return Result.Success(new EtrnExecuteResult { Error = known.Error, Exchanges = exchanges });

            var documentUrl = $"{api}/transedm/public/api/v1/etrns/{Uri.EscapeDataString(id)}/documents/{ConsigneeType}";
            var written = await CallAsync(
                HttpMethod.Put,
                documentUrl,
                token.Value.Token,
                bytes,
                "application/xml",
                null,
                EtrnMethods.OfdConsigneeTitle,
                id,
                xml,
                exchanges,
                cancellationToken);
            if (!written.Ok)
                return Result.Success(new EtrnExecuteResult { Error = written.Error, Exchanges = exchanges });

            // Платформа принимает PUT, но хранит свой документ. Подписывается сохранённый content, а не отправленные байты.
            var stored = await ConsigneeContentAsync(
                api,
                token.Value.Token,
                id,
                known.Value,
                exchanges,
                cancellationToken);
            if (!stored.Ok)
                return Result.Success(new EtrnExecuteResult { Error = stored.Error, Exchanges = exchanges });

            var signed = await _signer.SignAsync(new EtrnSignatureRequest
            {
                CertificateNumber = context.CertificateNumber,
                CertificatePassword = context.CertificatePassword,
                Content = stored.Bytes
            }, cancellationToken);
            if (signed.IsFailure)
                return Result.Success(new EtrnExecuteResult { Error = signed.Error, Exchanges = exchanges });

            var signature = new byte[signed.Value.Length];
            if (!Convert.TryFromBase64String(signed.Value, signature, out var writtenBytes))
            {
                return Result.Success(new EtrnExecuteResult
                {
                    Error = "Подпись титула не разобрана",
                    Exchanges = exchanges
                });
            }

            signature = signature[..writtenBytes];

            var signedCall = await CallAsync(
                HttpMethod.Put,
                documentUrl + "/signature",
                token.Value.Token,
                signature,
                "application/octet-stream",
                null,
                EtrnMethods.OfdConsigneeSignature,
                id,
                "",
                exchanges,
                cancellationToken);
            if (!signedCall.Ok)
                return Result.Success(new EtrnExecuteResult { Error = signedCall.Error, Exchanges = exchanges });

            return Result.Success(new EtrnExecuteResult { Completed = true, Exchanges = exchanges });
        }
        catch (Exception ex) when (IsTransport(ex, cancellationToken))
        {
            return Result.Success(new EtrnExecuteResult { Error = ex.Message, Exchanges = exchanges });
        }
    }

    public async Task<Result<EtrnStageResult>> ReadStageAsync(
        EtrnOperatorContext context,
        string operatorDocumentId,
        CancellationToken cancellationToken)
    {
        var exchanges = new List<EtrnExchange>();
        try
        {
            var ready = Ready(context);
            if (ready.IsFailure)
                return Result.Success(new EtrnStageResult { Error = ready.Error, Exchanges = exchanges });

            var token = await _tokens.AccessTokenAsync(context, cancellationToken);
            if (token.IsFailure)
                return Result.Success(new EtrnStageResult { Error = token.Error, Exchanges = exchanges });

            var id = (operatorDocumentId ?? "").Trim();
            var url = $"{context.Connection.ResolvedApiUrl()}/transedm/public/api/v1/etrns/{Uri.EscapeDataString(id)}";
            var card = await CallAsync(
                HttpMethod.Get,
                url,
                token.Value.Token,
                null,
                null,
                null,
                EtrnMethods.OfdCard,
                id,
                url,
                exchanges,
                cancellationToken);
            if (!card.Ok)
                return Result.Success(new EtrnStageResult { Error = card.Error, Exchanges = exchanges });

            var status = Status(card.Text);
            if (status.Length == 0)
            {
                return Result.Success(new EtrnStageResult
                {
                    Error = "Платформа ЭТрН не вернула статус ЭТрН",
                    Exchanges = exchanges
                });
            }

            return Result.Success(new EtrnStageResult
            {
                Completed = true,
                Stage = status,
                StillAcceptance = string.Equals(status, OnTheWay, StringComparison.OrdinalIgnoreCase),
                Exchanges = exchanges
            });
        }
        catch (Exception ex) when (IsTransport(ex, cancellationToken))
        {
            return Result.Success(new EtrnStageResult { Error = ex.Message, Exchanges = exchanges });
        }
    }

    private async Task<Result<EtrnFetchedDocument>> ReadShipperAsync(
        string api,
        string token,
        OfdCard card,
        List<EtrnExchange> exchanges,
        CancellationToken cancellationToken)
    {
        var documentsUrl = $"{api}/transedm/public/api/v1/etrns/{Uri.EscapeDataString(card.Id)}/documents";
        var documents = await CallAsync(
            HttpMethod.Get,
            documentsUrl,
            token,
            null,
            null,
            null,
            EtrnMethods.OfdTitles,
            card.Id,
            documentsUrl,
            exchanges,
            cancellationToken);
        if (!documents.Ok)
            return Result.Failure<EtrnFetchedDocument>(documents.Error);

        if (!TryShipperDocument(documents.Text, out var documentId))
            return Result.Failure<EtrnFetchedDocument>("Титул грузоотправителя не найден");

        var contentUrl = $"{api}/transedm/public/api/v1/documents/{Uri.EscapeDataString(documentId)}/content";
        var content = await CallAsync(
            HttpMethod.Get,
            contentUrl,
            token,
            null,
            null,
            "application/xml",
            EtrnMethods.OfdShipperTitle,
            card.Id,
            contentUrl,
            exchanges,
            cancellationToken);
        if (!content.Ok)
            return Result.Failure<EtrnFetchedDocument>(content.Error);

        var title = SenderTitleReader.Read(content.Bytes);
        if (title.IsFailure)
            return Result.Failure<EtrnFetchedDocument>(title.Error);

        return Result.Success(Overlay(title.Value, card));
    }

    /// <summary>
    /// Id титулов грузополучателя, уже лежащих на накладной до PUT.
    /// </summary>
    private async Task<Result<IReadOnlyCollection<string>>> ConsigneeIdsAsync(
        string api,
        string token,
        string etrnId,
        List<EtrnExchange> exchanges,
        CancellationToken cancellationToken)
    {
        var documentsUrl = ConsigneeDocumentsUrl(api, etrnId);
        var documents = await CallAsync(
            HttpMethod.Get,
            documentsUrl,
            token,
            null,
            null,
            null,
            EtrnMethods.OfdTitles,
            etrnId,
            documentsUrl,
            exchanges,
            cancellationToken);
        if (!documents.Ok)
            return Result.Failure<IReadOnlyCollection<string>>(documents.Error);

        if (!TryConsigneeDocuments(documents.Text, out var documentIds))
            return Result.Failure<IReadOnlyCollection<string>>("Платформа ЭТрН вернула не список титулов");

        return Result.Success<IReadOnlyCollection<string>>(documentIds);
    }

    /// <summary>
    /// После PUT титул появляется в списке документов не сразу. До пяти чтений списка, затем content
    /// только нового титула: старый документ уже подписан и его content подписывать нельзя.
    /// </summary>
    private async Task<OfdResponse> ConsigneeContentAsync(
        string api,
        string token,
        string etrnId,
        IReadOnlyCollection<string> knownDocumentIds,
        List<EtrnExchange> exchanges,
        CancellationToken cancellationToken)
    {
        var documentsUrl = ConsigneeDocumentsUrl(api, etrnId);
        for (var attempt = 1; attempt <= ConsigneeListReads; attempt++)
        {
            var documents = await CallAsync(
                HttpMethod.Get,
                documentsUrl,
                token,
                null,
                null,
                null,
                EtrnMethods.OfdTitles,
                etrnId,
                documentsUrl,
                exchanges,
                cancellationToken);
            if (!documents.Ok)
                return documents;

            if (TryConsigneeDocuments(documents.Text, out var documentIds))
            {
                var documentId = documentIds.FirstOrDefault(id => !knownDocumentIds.Contains(id));
                if (documentId != null)
                {
                    var contentUrl = $"{api}/transedm/public/api/v1/documents/{Uri.EscapeDataString(documentId)}/content";
                    var content = await CallAsync(
                        HttpMethod.Get,
                        contentUrl,
                        token,
                        null,
                        null,
                        "application/xml",
                        EtrnMethods.OfdConsigneeTitle,
                        etrnId,
                        contentUrl,
                        exchanges,
                        cancellationToken);
                    if (!content.Ok)
                        return content;
                    if (content.Bytes.Length == 0)
                        return new OfdResponse(false, "Платформа ЭТрН вернула пустой титул грузополучателя", [], "");

                    return content;
                }
            }

            if (attempt < ConsigneeListReads)
                await _pause(cancellationToken);
        }

        return new OfdResponse(false, "Новый титул грузополучателя не найден в Платформе ЭТрН", [], "");
    }

    private static string ConsigneeDocumentsUrl(string api, string etrnId) =>
        $"{api}/transedm/public/api/v1/etrns/{Uri.EscapeDataString(etrnId)}/documents";

    private async Task<OfdResponse> CallAsync(
        HttpMethod method,
        string url,
        string token,
        byte[]? body,
        string? contentType,
        string? accept,
        string name,
        string parameters,
        string requestBody,
        List<EtrnExchange> exchanges,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!string.IsNullOrEmpty(accept))
            request.Headers.Accept.ParseAdd(accept);
        if (body != null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        }

        var client = _http.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        var bytes = response.Content == null ? [] : await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var text = Encoding.UTF8.GetString(bytes);
        var ok = response.IsSuccessStatusCode;
        if (!ok)
            _logger.LogWarning(
                "Платформа ЭТрН {Method} ответила {Status}: {Body}",
                name,
                (int)response.StatusCode,
                EtrnDocumentState.Truncate(text));
        exchanges.Add(new EtrnExchange
        {
            Method = name,
            Parameters = parameters,
            RequestBody = requestBody,
            ResponseBody = text,
            Completed = ok,
            Error = ok ? "" : ((int)response.StatusCode).ToString()
        });
        return new OfdResponse(ok, ok ? "" : $"Платформа ЭТрН ответила {(int)response.StatusCode}", bytes, text);
    }

    private async Task<Result<string>> SubscriberIdAsync(
        EtrnOperatorContext context,
        string token,
        List<EtrnExchange> exchanges,
        CancellationToken cancellationToken)
    {
        var raw = context.Connection.SubscriberId.Trim();
        if (long.TryParse(raw, out _))
            return Result.Success(raw);

        var inn = (context.Inn ?? "").Trim();
        if (inn.Length == 0)
            return Result.Failure<string>("Не задан ИНН организации");

        var url = $"{context.Connection.ResolvedApiUrl()}/edm-subscriber/public/api/v3/subscribers?inn={Uri.EscapeDataString(inn)}";
        var answer = await CallAsync(
            HttpMethod.Get,
            url,
            token,
            null,
            null,
            null,
            EtrnMethods.OfdSubscriber,
            raw,
            url,
            exchanges,
            cancellationToken);
        if (!answer.Ok)
            return Result.Failure<string>(answer.Error);
        if (!TrySubscriberId(answer.Text, raw, out var id))
            return Result.Failure<string>("Абонент ЭДО не найден");
        return Result.Success(id);
    }

    private static bool TrySubscriberId(string json, string gid, out string id)
    {
        id = "";
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!SubscriberItems(document.RootElement, out var items))
                return false;

            foreach (var item in items)
            {
                if (!string.Equals(Text(item, "gid"), gid, StringComparison.OrdinalIgnoreCase))
                    continue;
                id = Id(item);
                return long.TryParse(id, out _);
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool SubscriberItems(JsonElement root, out JsonElement[] items)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            items = root.EnumerateArray().ToArray();
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array)
        {
            items = content.EnumerateArray().ToArray();
            return true;
        }

        items = [];
        return false;
    }

    private static string TokenNote(int expiresIn) => $"Токен получен, действует {expiresIn} с";

    private static Result<EtrnSignerName> ReadSigner(IOrganizationCertificateDirectory certificates, string serial)
    {
        var found = certificates.Find(serial);
        if (found.IsFailure)
            return Result.Failure<EtrnSignerName>(found.Error);

        using var certificate = found.Value.Certificate;
        var read = CertificateSubjectName.Read(certificate.Subject);
        if (read.IsFailure)
            return Result.Failure<EtrnSignerName>(read.Error);

        return Result.Success(new EtrnSignerName
        {
            Surname = read.Value.Surname,
            Name = read.Value.Name,
            Patronymic = read.Value.Patronymic
        });
    }

    private static Result Ready(EtrnOperatorContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Connection.SubscriberId))
            return Result.Failure("Не указан абонент ЭДО");
        if (string.IsNullOrWhiteSpace(context.CertificateNumber))
            return Result.Failure("Не задан сертификат организации");
        var missing = context.Connection.MissingOfdAddress();
        if (missing.Length > 0)
            return Result.Failure(missing);
        return Result.Success();
    }

    private static EtrnProbeResult ProbeStopped(string error, List<EtrnExchange> exchanges) => new()
    {
        Ok = false,
        Message = error,
        Exchanges = exchanges
    };

    /// <summary>
    /// number — размер страницы, а не номер накладной. Следующая страница идёт назад от минимального id.
    /// </summary>
    private static string ListUrl(string api, string subscriber, string? fromId)
    {
        var url = $"{api}/transedm/public/api/v1/etrns?subscriber-id={Uri.EscapeDataString(subscriber)}&number=-{PageSize}";
        if (!string.IsNullOrEmpty(fromId))
            url += "&from-id=" + Uri.EscapeDataString(fromId);
        return url;
    }

    private static string MinId(IReadOnlyList<OfdCard> cards)
    {
        long? minimum = null;
        var text = "";
        foreach (var card in cards)
        {
            if (!long.TryParse(card.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                continue;

            if (minimum == null || id < minimum)
            {
                minimum = id;
                text = id.ToString(CultureInfo.InvariantCulture);
            }
        }

        return text;
    }

    private static EtrnFetchResult Stopped(EtrnPollPosition position, List<EtrnExchange> exchanges, string error) => new()
    {
        Completed = false,
        Error = error,
        Position = position,
        Exchanges = exchanges
    };

    private static bool TryCards(string json, out List<OfdCard> cards)
    {
        cards = [];
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                cards = root.EnumerateArray().Select(Card).ToList();
                return true;
            }

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.Array)
            {
                cards = content.EnumerateArray().Select(Card).ToList();
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static OfdCard Card(JsonElement item)
    {
        var status = Text(item, "status");
        return new OfdCard(
            Id(item),
            status,
            string.Equals(status, OnTheWay, StringComparison.OrdinalIgnoreCase),
            Text(item, "number"),
            Text(item, "uid"),
            Address(item));
    }

    private static string Address(JsonElement item)
    {
        if (!item.TryGetProperty("destAddress", out var address))
            return "";
        if (address.ValueKind == JsonValueKind.String)
            return (address.GetString() ?? "").Trim();
        if (address.ValueKind != JsonValueKind.Object)
            return "";

        var parts = new[] { "postCode", "regionCode", "district", "settlement", "street", "building" }
            .Select(name => Text(address, name))
            .Where(part => part.Length > 0);
        return string.Join(", ", parts);
    }

    private static bool TryConsigneeDocuments(string json, out List<string> documentIds)
    {
        documentIds = [];
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array
                && !(root.ValueKind == JsonValueKind.Object
                     && (root.TryGetProperty("content", out _) || root.TryGetProperty("knd", out _))))
            {
                return false;
            }

            foreach (var item in Documents(root))
            {
                if (!Text(item, "knd").Contains(ConsigneeKnd, StringComparison.Ordinal))
                    continue;

                var documentId = Id(item);
                if (documentId.Length > 0)
                    documentIds.Add(documentId);
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryShipperDocument(string json, out string documentId)
    {
        documentId = "";
        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var item in Documents(document.RootElement))
            {
                if (!string.Equals(Text(item, "knd"), ShipperKnd, StringComparison.Ordinal))
                    continue;
                documentId = Id(item);
                return documentId.Length > 0;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static IEnumerable<JsonElement> Documents(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().ToList();
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array)
            return content.EnumerateArray().ToList();
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("knd", out _))
            return [root];
        return [];
    }

    private static string Status(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Text(document.RootElement, "status")
                : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static EtrnFetchedDocument Overlay(SenderTitle title, OfdCard card)
    {
        var fetched = title.ToFetched(card.Id);
        return new EtrnFetchedDocument
        {
            OperatorDocumentId = fetched.OperatorDocumentId,
            Number = First(fetched.Number, card.Number),
            Date = fetched.Date,
            Consignor = fetched.Consignor,
            Uid = First(fetched.Uid, card.Uid),
            DeliveryAddress = First(fetched.DeliveryAddress, card.Address),
            RawXml = fetched.RawXml,
            Lines = fetched.Lines,
            Error = fetched.Error
        };
    }

    private static string First(string current, string fallback) =>
        string.IsNullOrWhiteSpace(current) ? fallback : current;

    private static string Id(JsonElement item)
    {
        if (!item.TryGetProperty("id", out var id))
            return "";
        return id.ValueKind switch
        {
            JsonValueKind.Number => id.GetRawText(),
            JsonValueKind.String => (id.GetString() ?? "").Trim(),
            _ => ""
        };
    }

    private static string Text(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
            return "";
        return value.ValueKind switch
        {
            JsonValueKind.String => (value.GetString() ?? "").Trim(),
            JsonValueKind.Number => value.GetRawText(),
            _ => ""
        };
    }

    private static bool IsTransport(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private sealed record OfdCard(string Id, string Status, bool OnTheWay, string Number, string Uid, string Address);

    private sealed record OfdResponse(bool Ok, string Error, byte[] Bytes, string Text);
}
