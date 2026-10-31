using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Domain.Vetis;
using Microsoft.Extensions.Logging;
using VetisApi.Xml;

namespace VetisApi;

/// <summary>
/// Заявки ApplicationManagementService и список площадок EnterpriseService. Пароль в журнал не пишется.
/// </summary>
public sealed class VetisGateway : IVetisGateway
{
    public const string HttpClientName = "Vetis";

    private const int MaxEnterprisePages = 10;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VetisGateway> _logger;

    public VetisGateway(IHttpClientFactory httpClientFactory, ILogger<VetisGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<VetisExchange> CheckConnectionAsync(VetisCall call, CancellationToken cancellationToken)
    {
        return SubmitAndWaitAsync(
            call,
            GetBusinessEntityUserXml.Operation,
            GetBusinessEntityUserXml.Create(call),
            $"Оператор {call.InitiatorLogin.Trim()}",
            cancellationToken);
    }

    public async Task<VetisIncomingResult> GetIncomingAsync(
        VetisCall call,
        VetisIncomingRequest request,
        CancellationToken cancellationToken)
    {
        var exchange = await SubmitAndWaitAsync(
            call,
            GetVetDocumentChangesListXml.Operation,
            GetVetDocumentChangesListXml.Create(call, request),
            GetVetDocumentChangesListXml.Describe(request),
            cancellationToken);
        if (!exchange.Completed)
            return new VetisIncomingResult { Exchange = exchange };

        return new VetisIncomingResult
        {
            Exchange = exchange,
            Documents = VetisResponseReader.ReadIncoming(exchange.ResponseXml, request.EnterpriseGuid),
            PageCount = VetisResponseReader.ListedCount(exchange.ResponseXml)
        };
    }

    public async Task<VetisQuenchResult> QuenchAsync(
        VetisCall call,
        string rawXml,
        VetisQuenchOrder order,
        string uuid,
        CancellationToken cancellationToken)
    {
        var built = ProcessIncomingConsignmentXml.Create(call, rawXml, order, DateOnly.FromDateTime(DateTime.Now));
        if (built.IsFailure)
        {
            return new VetisQuenchResult
            {
                Exchange = new VetisExchange
                {
                    Operation = ProcessIncomingConsignmentXml.Operation,
                    Parameters = ProcessIncomingConsignmentXml.Describe(uuid, order),
                    Status = "NOT_SENT",
                    Completed = false,
                    Error = built.Error
                }
            };
        }

        var exchange = await SubmitAndWaitAsync(
            call,
            ProcessIncomingConsignmentXml.Operation,
            built.Value,
            ProcessIncomingConsignmentXml.Describe(uuid, order),
            cancellationToken);
        var quenched = VetisResponseReader.ReadQuenched(exchange.ResponseXml, uuid);
        return new VetisQuenchResult
        {
            Exchange = exchange,
            Status = quenched?.Status ?? "",
            ReceivedAt = quenched?.ReceivedAt ?? ""
        };
    }

    public async Task<VetisEnterpriseList> ListEnterprisesAsync(VetisCall call, CancellationToken cancellationToken)
    {
        var url = VetisEndpoints.EnterpriseService(call.Endpoint);
        var collected = new List<VetisEnterprise>();
        var exchanges = new List<VetisExchange>();
        var offset = 0;

        for (var page = 0; page < MaxEnterprisePages; page++)
        {
            var envelope = VetisEnvelope.Registry(
                GetActivityLocationListXml.Create(call.IssuerId, GetActivityLocationListXml.PageSize, offset));
            var posted = await PostAsync(call, envelope, GetActivityLocationListXml.Operation, url, cancellationToken, cancellationToken);
            var fault = posted.Ok ? VetisResponseReader.ErrorText(posted.ResponseXml) : null;
            var failure = posted.Ok ? fault : posted.Error ?? "Ветис не вернул список площадок.";
            exchanges.Add(new VetisExchange
            {
                Operation = GetActivityLocationListXml.Operation,
                Parameters = $"Площадки: смещение {offset}, количество {GetActivityLocationListXml.PageSize}",
                Status = failure == null ? "COMPLETED" : posted.Sent ? "FAILED" : "NOT_SENT",
                Completed = failure == null,
                Error = failure,
                RequestXml = VetisRequestLog.Masked(envelope),
                ResponseXml = posted.ResponseXml
            });
            if (failure != null)
            {
                return new VetisEnterpriseList
                {
                    Completed = false,
                    Error = failure,
                    Exchanges = exchanges
                };
            }

            var batch = VetisResponseReader.ReadEnterprises(posted.ResponseXml);
            collected.AddRange(batch);
            offset += batch.Count;
            var total = VetisResponseReader.ListTotal(posted.ResponseXml, "activityLocationList");
            var ceiling = MaxEnterprisePages * GetActivityLocationListXml.PageSize;
            if (total != null && total > ceiling)
            {
                return new VetisEnterpriseList
                {
                    Completed = false,
                    Error = $"Ветис сообщил {total} площадок. За один запрос можно забрать не больше {ceiling}.",
                    Exchanges = exchanges
                };
            }

            if (batch.Count == 0 || batch.Count < GetActivityLocationListXml.PageSize || (total != null && offset >= total))
                break;
        }

        var enterprises = collected
            .GroupBy(item => item.Guid, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        _logger.LogInformation("Список площадок Ветис: {Count}", enterprises.Count);
        return new VetisEnterpriseList
        {
            Completed = true,
            Enterprises = enterprises,
            Exchanges = exchanges
        };
    }

    private async Task<VetisExchange> SubmitAndWaitAsync(
        VetisCall call,
        string operation,
        XElement applicationData,
        string parameters,
        CancellationToken cancellationToken)
    {
        var envelope = VetisEnvelope.Submit(call, applicationData);
        var sent = new SentRequest(
            operation,
            applicationData.Element(VetisNs.Mercury + "localTransactionId")?.Value ?? "",
            parameters,
            VetisRequestLog.Masked(envelope));

        // Иначе каждый опрос заново получает TimeoutSeconds, и ACCEPTED держит запрос минутами.
        var budgetSeconds = call.TimeoutSeconds > 0 ? call.TimeoutSeconds : 60;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));

        string? applicationId = null;
        var status = "";
        var responseXml = "";
        try
        {
            var submitted = await PostAsync(call, envelope, VetisEnvelope.SubmitAction, call.Endpoint, budget.Token, cancellationToken);
            if (submitted.TimedOut)
                return TimedOut(sent, applicationId, status, budgetSeconds, responseXml);
            if (!submitted.Ok)
            {
                return sent.Exchange(
                    completed: false,
                    status: submitted.Sent ? "FAILED" : "NOT_SENT",
                    error: submitted.Error,
                    responseXml: submitted.ResponseXml);
            }

            responseXml = submitted.ResponseXml;
            applicationId = VetisResponseReader.ApplicationId(responseXml);
            status = VetisResponseReader.ApplicationStatus(responseXml) ?? "";
            var attempts = Math.Max(1, call.ResultPollAttempts);

            for (var attempt = 0; attempt < attempts && status is "ACCEPTED" or "IN_PROCESS"; attempt++)
            {
                if (string.IsNullOrWhiteSpace(applicationId))
                    break;

                if (budget.IsCancellationRequested)
                    return TimedOut(sent, applicationId, status, budgetSeconds, responseXml);

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, call.ResultPollDelaySeconds)), budget.Token);
                var receive = VetisEnvelope.Receive(call, applicationId);
                var polled = await PostAsync(call, receive, VetisEnvelope.ReceiveAction, call.Endpoint, budget.Token, cancellationToken);
                if (polled.TimedOut)
                    return TimedOut(sent, applicationId, status, budgetSeconds, responseXml);
                if (!polled.Ok)
                {
                    return sent.Exchange(
                        completed: false,
                        status: status,
                        error: polled.Error,
                        responseXml: polled.ResponseXml.Length > 0 ? polled.ResponseXml : responseXml,
                        applicationId: applicationId);
                }

                responseXml = polled.ResponseXml;
                applicationId = VetisResponseReader.ApplicationId(responseXml) ?? applicationId;
                status = VetisResponseReader.ApplicationStatus(responseXml) ?? status;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TimedOut(sent, applicationId, status, budgetSeconds, responseXml);
        }

        _logger.LogInformation("Заявка Ветис {ApplicationId} по операции {Operation}: {Status}", applicationId, operation, status);
        var error = VetisResponseReader.ErrorText(responseXml);
        var completed = status == "COMPLETED" && string.IsNullOrWhiteSpace(error);
        if (!completed && string.IsNullOrWhiteSpace(error))
        {
            error = string.IsNullOrWhiteSpace(applicationId)
                ? "Ветис не вернул applicationId."
                : $"Заявка {applicationId} не завершена, статус {status}.";
        }

        return sent.Exchange(
            completed,
            string.IsNullOrWhiteSpace(status) ? "UNKNOWN" : status,
            completed ? null : error,
            responseXml,
            applicationId);
    }

    private static VetisExchange TimedOut(SentRequest sent, string? applicationId, string status, int seconds, string responseXml)
    {
        var state = string.IsNullOrWhiteSpace(status) ? "UNKNOWN" : status;
        var error = string.IsNullOrWhiteSpace(applicationId)
            ? $"Ветис не ответил за {seconds} с."
            : $"Заявка {applicationId} не успела завершиться за {seconds} с, статус {state}.";
        return sent.Exchange(completed: false, status: state, error: error, responseXml: responseXml, applicationId: applicationId);
    }

    /// <summary>
    /// Таймаут берётся из вызова: именованный HttpClient живёт дольше одной заявки.
    /// </summary>
    private async Task<PostOutcome> PostAsync(
        VetisCall call,
        XElement envelope,
        string soapAction,
        string url,
        CancellationToken operationToken,
        CancellationToken callerToken)
    {
        var requestXml = VetisNs.ToUtf8Xml(envelope);
        var target = (url ?? "").Trim();
        _logger.LogInformation("Вызов Ветис {Action}", soapAction);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(operationToken);
        var seconds = call.TimeoutSeconds > 0 ? call.TimeoutSeconds : 60;
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        try
        {
            using var http = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, target);
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{call.BasicLogin}:{call.BasicPassword}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Headers.TryAddWithoutValidation("SOAPAction", soapAction);
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(requestXml));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

            using var response = await http.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var fault = VetisResponseReader.ErrorText(body) ?? body;
                var details = Compact(fault);
                return new PostOutcome
                {
                    Sent = true,
                    ResponseXml = body,
                    Error = details.Length == 0
                        ? $"Ветис вернул {(int)response.StatusCode}."
                        : $"Ветис вернул {(int)response.StatusCode}: {details}"
                };
            }

            return new PostOutcome { Ok = true, Sent = true, ResponseXml = body };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (callerToken.IsCancellationRequested)
                throw;

            if (operationToken.IsCancellationRequested)
                return new PostOutcome { TimedOut = true };

            _logger.LogError(ex, "Ошибка вызова Ветис {Action}", soapAction);
            return new PostOutcome
            {
                Error = $"Нет связи с Ветис ({target}): {ex.Message}"
            };
        }
    }

    private static string Compact(string value)
    {
        var compact = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return compact.Length <= 2000 ? compact : compact[..2000];
    }

    private sealed record SentRequest(string Operation, string LocalTransactionId, string Parameters, string RequestXml)
    {
        public VetisExchange Exchange(
            bool completed,
            string status,
            string? error,
            string responseXml,
            string? applicationId = null) =>
            new()
            {
                Completed = completed,
                ApplicationId = applicationId,
                Status = status,
                Error = error,
                Operation = Operation,
                LocalTransactionId = LocalTransactionId,
                Parameters = Parameters,
                RequestXml = RequestXml,
                ResponseXml = responseXml
            };
    }

    private sealed class PostOutcome
    {
        public bool Ok { get; init; }

        public bool TimedOut { get; init; }

        public bool Sent { get; init; }

        public string ResponseXml { get; init; } = "";

        public string? Error { get; init; }
    }
}
