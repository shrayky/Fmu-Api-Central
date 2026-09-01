using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.GisMt.Dto;
using Domain.GisMt.Enum;
using Domain.GisMt.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Http;
using Shared.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GisMtExchange.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class GisMtClient : IGisMtClient
{
    public const string HttpClientName = "GisMt";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GisMtClient> _logger;

    public GisMtClient(IHttpClientFactory httpClientFactory, ILogger<GisMtClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Отправляет пакет настроек на GisMt.
    /// </summary>
    public async Task<Result> PutExchangeConfiguration(
        string serviceUrl,
        GisMtConfigurationPacket packet,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(packet, JsonSerializeOptionsProvider.Default());
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var send = await Send(HttpMethod.Put, Combine(serviceUrl, "/api/exchange-state"), content, cancellationToken);
        return send.IsSuccess ? Result.Success() : Result.Failure(send.Error);
    }

    /// <summary>
    /// Читает статусы организаций с GisMt.
    /// </summary>
    public async Task<Result<GisMtStatusResponse>> FmuApiGisMtStatus(
        string serviceUrl,
        CancellationToken cancellationToken)
    {
        var send = await Send(HttpMethod.Get, Combine(serviceUrl, "/api/status"), null, cancellationToken);
        if (send.IsFailure)
            return Result.Failure<GisMtStatusResponse>(send.Error);

        return Result.Success(DeserializeStatus(send.Value));
    }

    /// <summary>
    /// Отправляет ручную операцию ГИС МТ.
    /// </summary>
    public async Task<Result> Operation(
        string serviceUrl,
        GisMtManualOperationKind kind,
        GisMtManualOperationRequest request,
        CancellationToken cancellationToken)
    {
        var path = kind switch
        {
            GisMtManualOperationKind.ProductGroups => "/api/gismt/product-groups",
            GisMtManualOperationKind.Documents => "/api/gismt/documents",
            GisMtManualOperationKind.Stock => "/api/gismt/stock",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестная операция ГИС МТ")
        };

        var json = JsonSerializer.Serialize(request, JsonSerializeOptionsProvider.Default());
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var send = await Send(HttpMethod.Post, Combine(serviceUrl, path), content, cancellationToken);
        return send.IsSuccess ? Result.Success() : Result.Failure(send.Error);
    }

    /// <summary>
    /// Выполняет HTTP-запрос к GisMt и возвращает тело ответа.
    /// </summary>
    private async Task<Result<string>> Send(
        HttpMethod method,
        string url,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var clientResult = _httpClientFactory.CreateClientSafely(HttpClientName, _logger);
        if (clientResult.IsFailure)
            return Result.Failure<string>(clientResult.Error);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = content;

        _logger.LogInformation("{Method} {Url}", method, url);

        var sendResult = await clientResult.Value.SendRequestSafelyAsync(
            client => client.SendAsync(request, cancellationToken),
            _logger,
            $"{method} {url}");

        if (sendResult.IsFailure)
            return Result.Failure<string>(sendResult.Error);

        using var response = sendResult.Value;
        var code = (int)response.StatusCode;
        _logger.LogInformation("{Method} {Url} -> {Code}", method, url, code);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (code >= 200 && code < 300)
            return Result.Success(body);

        var message = $"{code} {url}";
        if (code >= 500)
            _logger.LogWarning("GisMt недоступен: {Message}", message);
        else
            _logger.LogError("GisMt отклонил запрос: {Message}", message);

        return Result.Failure<string>(message);
    }

    /// <summary>
    /// Собирает абсолютный URL метода GisMt из базового адреса сервиса.
    /// </summary>
    private static string Combine(string serviceUrl, string path)
    {
        var baseUrl = (serviceUrl ?? string.Empty).Trim().TrimEnd('/');
        var relative = path.StartsWith('/') ? path : "/" + path;
        return baseUrl + relative;
    }

    /// <summary>
    /// Разбирает тело статуса; при пустом или битом JSON возвращает пустой снимок.
    /// </summary>
    private static GisMtStatusResponse DeserializeStatus(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new GisMtStatusResponse();

        try
        {
            return JsonSerializer.Deserialize<GisMtStatusResponse>(body, JsonSerializeOptionsProvider.Default())
                ?? new GisMtStatusResponse();
        }
        catch (JsonException)
        {
            return new GisMtStatusResponse();
        }
    }
}
