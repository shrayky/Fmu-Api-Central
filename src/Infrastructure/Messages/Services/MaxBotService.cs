using CSharpFunctionalExtensions;
using Domain.Bot;
using Microsoft.Extensions.Logging;
using Shared.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Messages.Services;

public class MaxBotService : IMessageService
{
    private readonly ILogger<MaxBotService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string ApiAddress = "https://platform-api.max.ru";

    public MaxBotService(ILogger<MaxBotService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Result> Send(string access_token, long chatId, string message)
    {
        var httpClientResult = _httpClientFactory.CreateClientSafely("messageBot", _logger);

        if (httpClientResult.IsFailure)
        {
            _logger.LogError("Не удалось создать HttpClient: {Error}", httpClientResult.Error);
            return Result.Failure(httpClientResult.Error);
        }

        using var httpClient = httpClientResult.Value;
        httpClient.BaseAddress = new Uri(ApiAddress);

        message = message.Replace("%0A", "\r\n");

        var payload = new
        {
            text = message,
            format = "html"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"/messages?chat_id={chatId}");
        request.Headers.Authorization = new AuthenticationHeaderValue(access_token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var responseResult = await httpClient.SendRequestSafelyAsync(
            client => client.SendAsync(request),
            _logger,
            "Отправка сообщения в Макс"
        );

        if (responseResult.IsSuccess)
        {
            if (!responseResult.Value.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Макс-бот: отправка сообщения вернула код {Code}",
                    responseResult.Value.StatusCode);
            }
        }

        return responseResult.IsSuccess ? Result.Success() : Result.Failure(responseResult.Error);
    }
}
