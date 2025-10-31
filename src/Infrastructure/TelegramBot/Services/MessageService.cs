using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Bot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Http;

namespace TelegramBot.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class MessageService : IMessageService
{
    private readonly ILogger<MessageService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    
    private const string ApiAddress = "https://api.telegram.org";

    public MessageService(ILogger<MessageService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Result> Send(string botId, long chatId, string message)
    {
        var httpClientResult = _httpClientFactory.CreateClientSafely("telegramBot", _logger);

        if (httpClientResult.IsFailure)
        {
            _logger.LogError("Не удалось создать HttpClient: {Error}", httpClientResult.Error);
            return Result.Failure(httpClientResult.Error);
        }
        
        using var httpClient = httpClientResult.Value;
        httpClient.BaseAddress = new Uri(ApiAddress);
        
        var responseResult = await httpClient.SendRequestSafelyAsync(
            client => client.GetAsync($"/bot{botId}/sendMessage?chat_id=-{chatId}&parse_mode=HTML&text={message}"),
            _logger,
            $"Отправка сообщения в чат");

        if (responseResult.IsSuccess)
        {
            if (!responseResult.Value.IsSuccessStatusCode)
                _logger.LogError("Телеграм бот: отправка сообщения вернула код {code}", responseResult.Value.StatusCode);
        }
        
        return responseResult.IsSuccess ? Result.Success() : Result.Failure(responseResult.Error);
    }
}