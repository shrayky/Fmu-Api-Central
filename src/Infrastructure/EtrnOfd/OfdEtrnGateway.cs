using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using Domain.Etrn.Xml;
using Microsoft.Extensions.DependencyInjection;

namespace EtrnOfd;

public sealed class OfdEtrnGateway : IEtrnOperator
{
    public const string HttpClientName = "EtrnOfd";
    public const string OnTheWay = "CARGO_ON_WAY";

    private const string ShipperKnd = "1110339";
    private const string ConsigneeType = "1110341v5.01";

    private readonly IHttpClientFactory _http;
    private readonly OfdTokenClient _tokens;
    private readonly IEtrnSignaturePort _signer;
    private readonly Func<DateTimeOffset> _now;

    [ActivatorUtilitiesConstructor]
    public OfdEtrnGateway(
        IHttpClientFactory http,
        OfdTokenClient tokens,
        IEtrnSignaturePort signer)
        : this(http, tokens, signer, () => DateTimeOffset.Now)
    {
    }

    public OfdEtrnGateway(
        IHttpClientFactory http,
        OfdTokenClient tokens,
        IEtrnSignaturePort signer,
        Func<DateTimeOffset> clock)
    {
        _http = http;
        _tokens = tokens;
        _signer = signer;
        _now = clock;
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

            if (string.IsNullOrWhiteSpace(context.Connection.ClientId))
                return Result.Success(ProbeStopped("Не задан client_id Платформы ОФД", exchanges));

            var token = await _tokens.AccessTokenAsync(context.Connection, fresh: true, cancellationToken);
            if (token.IsFailure)
                return Result.Success(ProbeStopped(token.Error, exchanges));

            var subscriber = context.Connection.SubscriberId.Trim();
            var listUrl = $"{context.Connection.ResolvedApiUrl()}/transedm/public/api/v1/etrns?subscriber-id={Uri.EscapeDataString(subscriber)}";
            var list = await CallAsync(
                HttpMethod.Get,
                listUrl,
                token.Value,
                null,
                null,
                null,
                EtrnMethods.Probe,
                subscriber,
                "",
                exchanges,
                cancellationToken);
            if (!list.Ok)
                return Result.Success(ProbeStopped(list.Error, exchanges));
            if (!TryCards(list.Text, out _))
                return Result.Success(ProbeStopped("ОФД вернула не список ЭТрН", exchanges));

            return Result.Success(new EtrnProbeResult
            {
                Ok = true,
                Message = "Платформа ОФД приняла подключение",
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

            var token = await _tokens.AccessTokenAsync(context.Connection, cancellationToken);
            if (token.IsFailure)
                return Result.Success(Stopped(context.Position, exchanges, token.Error));

            var api = context.Connection.ResolvedApiUrl();
            var subscriber = context.Connection.SubscriberId.Trim();
            var listUrl = $"{api}/transedm/public/api/v1/etrns?subscriber-id={Uri.EscapeDataString(subscriber)}";
            var list = await CallAsync(
                HttpMethod.Get,
                listUrl,
                token.Value,
                null,
                null,
                null,
                "Список ЭТрН",
                subscriber,
                listUrl,
                exchanges,
                cancellationToken);
            if (!list.Ok)
                return Result.Success(Stopped(context.Position, exchanges, list.Error));
            if (!TryCards(list.Text, out var cards))
                return Result.Success(Stopped(context.Position, exchanges, "ОФД вернула не список ЭТрН"));

            var documents = new List<EtrnFetchedDocument>();
            var closed = new List<string>();
            foreach (var card in cards)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!card.OnTheWay)
                {
                    if (card.Id.Length > 0)
                        closed.Add(card.Id);
                    continue;
                }

                var title = await ReadShipperAsync(api, token.Value, card, exchanges, cancellationToken);
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

            var token = await _tokens.AccessTokenAsync(context.Connection, cancellationToken);
            if (token.IsFailure)
                return Result.Success(new EtrnExecuteResult { Error = token.Error, Exchanges = exchanges });

            var xml = ConsigneeTitleWriter.Build(command.Document, command.Decision, _now());
            var bytes = ConsigneeTitleWriter.ToUtf8(xml);
            var id = (command.Document.OperatorDocumentId ?? "").Trim();
            var documentUrl = $"{context.Connection.ResolvedApiUrl()}/transedm/public/api/v1/etrns/{Uri.EscapeDataString(id)}/documents/{ConsigneeType}";
            var written = await CallAsync(
                HttpMethod.Put,
                documentUrl,
                token.Value,
                bytes,
                "application/xml",
                null,
                "Титул грузополучателя",
                id,
                xml,
                exchanges,
                cancellationToken);
            if (!written.Ok)
                return Result.Success(new EtrnExecuteResult { Error = written.Error, Exchanges = exchanges });

            var signed = await _signer.SignAsync(new EtrnSignatureRequest
            {
                CertificateNumber = context.CertificateNumber,
                CertificatePassword = context.CertificatePassword,
                Content = bytes
            }, cancellationToken);
            if (signed.IsFailure)
                return Result.Success(new EtrnExecuteResult { Error = signed.Error, Exchanges = exchanges });

            byte[] signature;
            try
            {
                signature = Convert.FromBase64String(signed.Value);
            }
            catch (FormatException)
            {
                return Result.Success(new EtrnExecuteResult
                {
                    Error = "Подпись титула не разобрана",
                    Exchanges = exchanges
                });
            }

            var signedCall = await CallAsync(
                HttpMethod.Put,
                documentUrl + "/signature",
                token.Value,
                signature,
                "application/octet-stream",
                null,
                "Подпись титула грузополучателя",
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

            var token = await _tokens.AccessTokenAsync(context.Connection, cancellationToken);
            if (token.IsFailure)
                return Result.Success(new EtrnStageResult { Error = token.Error, Exchanges = exchanges });

            var id = (operatorDocumentId ?? "").Trim();
            var url = $"{context.Connection.ResolvedApiUrl()}/transedm/public/api/v1/etrns/{Uri.EscapeDataString(id)}";
            var card = await CallAsync(
                HttpMethod.Get,
                url,
                token.Value,
                null,
                null,
                null,
                "Карточка ЭТрН",
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
                    Error = "ОФД не вернула статус ЭТрН",
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
            "Титулы ЭТрН",
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
            "Титул грузоотправителя",
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
        exchanges.Add(new EtrnExchange
        {
            Method = name,
            Parameters = parameters,
            RequestBody = requestBody,
            ResponseBody = text,
            Completed = ok,
            Error = ok ? "" : ((int)response.StatusCode).ToString()
        });
        return new OfdResponse(ok, ok ? "" : $"ОФД ответила {(int)response.StatusCode}", bytes, text);
    }

    private static Result Ready(EtrnOperatorContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Connection.SubscriberId))
            return Result.Failure("Не указан абонент ЭДО");
        if (string.IsNullOrWhiteSpace(context.Connection.ApiKey))
            return Result.Failure("Не задан API-ключ Платформы ОФД");
        return Result.Success();
    }

    private static EtrnProbeResult ProbeStopped(string error, List<EtrnExchange> exchanges) => new()
    {
        Ok = false,
        Message = error,
        Exchanges = exchanges
    };

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
