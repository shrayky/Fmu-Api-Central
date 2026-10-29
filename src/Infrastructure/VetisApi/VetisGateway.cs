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
        var envelope = VetisEnvelope.Submit(call, GetBusinessEntityUserXml.Create(call));
        return SubmitAndWaitAsync(call, GetBusinessEntityUserXml.Operation, envelope, cancellationToken);
    }

    public async Task<VetisEnterpriseList> ListEnterprisesAsync(VetisCall call, CancellationToken cancellationToken)
    {
        var url = VetisEndpoints.EnterpriseService(call.Endpoint);
        var collected = new List<VetisEnterprise>();
        var offset = 0;

        for (var page = 0; page < MaxEnterprisePages; page++)
        {
            var envelope = VetisEnvelope.Registry(
                GetActivityLocationListXml.Create(call.IssuerId, GetActivityLocationListXml.PageSize, offset));
            var posted = await PostAsync(call, envelope, GetActivityLocationListXml.Operation, url, cancellationToken, cancellationToken);
            if (!posted.Ok)
            {
                return new VetisEnterpriseList
                {
                    Completed = false,
                    Error = posted.Error ?? "Ветис не вернул список площадок."
                };
            }

            var fault = VetisResponseReader.ErrorText(posted.ResponseXml);
            if (!string.IsNullOrWhiteSpace(fault))
            {
                return new VetisEnterpriseList
                {
                    Completed = false,
                    Error = fault
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
                    Error = $"Ветис сообщил {total} площадок. За один запрос можно забрать не больше {ceiling}."
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
            Enterprises = enterprises
        };
    }

    private async Task<VetisExchange> SubmitAndWaitAsync(
        VetisCall call,
        string operation,
        XElement envelope,
        CancellationToken cancellationToken)
    {
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
                return TimedOut(applicationId, status, budgetSeconds);
            if (!submitted.Ok)
            {
                return new VetisExchange
                {
                    Status = submitted.Sent ? "FAILED" : "NOT_SENT",
                    Error = submitted.Error,
                    Completed = false
                };
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
                    return TimedOut(applicationId, status, budgetSeconds);

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, call.ResultPollDelaySeconds)), budget.Token);
                var receive = VetisEnvelope.Receive(call, applicationId);
                var polled = await PostAsync(call, receive, VetisEnvelope.ReceiveAction, call.Endpoint, budget.Token, cancellationToken);
                if (polled.TimedOut)
                    return TimedOut(applicationId, status, budgetSeconds);
                if (!polled.Ok)
                {
                    return new VetisExchange
                    {
                        ApplicationId = applicationId,
                        Status = status,
                        Error = polled.Error,
                        Completed = false
                    };
                }

                responseXml = polled.ResponseXml;
                applicationId = VetisResponseReader.ApplicationId(responseXml) ?? applicationId;
                status = VetisResponseReader.ApplicationStatus(responseXml) ?? status;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TimedOut(applicationId, status, budgetSeconds);
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

        return new VetisExchange
        {
            ApplicationId = applicationId,
            Status = string.IsNullOrWhiteSpace(status) ? "UNKNOWN" : status,
            Error = completed ? null : error,
            Completed = completed
        };
    }

    private static VetisExchange TimedOut(string? applicationId, string status, int seconds)
    {
        var state = string.IsNullOrWhiteSpace(status) ? "UNKNOWN" : status;
        var error = string.IsNullOrWhiteSpace(applicationId)
            ? $"Ветис не ответил за {seconds} с."
            : $"Заявка {applicationId} не успела завершиться за {seconds} с, статус {state}.";
        return new VetisExchange
        {
            ApplicationId = applicationId,
            Status = state,
            Error = error,
            Completed = false
        };
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

    private sealed class PostOutcome
    {
        public bool Ok { get; init; }

        public bool TimedOut { get; init; }

        public bool Sent { get; init; }

        public string ResponseXml { get; init; } = "";

        public string? Error { get; init; }
    }
}
