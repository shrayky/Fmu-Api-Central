using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Dadata;

namespace Dadata;

/// <summary>
/// HTTP-клиенты DaData: подсказки адреса и стандартизация. Ключи читаются из Parameters на каждый запрос.
/// </summary>
public sealed class DadataAddressClient : IDadataAddressClient
{
    public const string HttpClientName = "dadata";

    private const string SuggestUrl = "https://suggestions.dadata.ru/suggestions/api/4_1/rs/suggest/address";
    private const string CleanUrl = "https://cleaner.dadata.ru/api/v1/clean/address";

    // Подсказки приходят списком домов: этого хватает, чтобы выбрать нужный.
    private const int SuggestCount = 20;

    private readonly IHttpClientFactory _http;
    private readonly IParametersService _parameters;

    public DadataAddressClient(IHttpClientFactory http, IParametersService parameters)
    {
        _http = http;
        _parameters = parameters;
    }

    public async Task<Result<IReadOnlyList<DadataSuggestion>>> Suggest(
        string address,
        CancellationToken cancellationToken)
    {
        var settings = await ReadSettings();
        if (!settings.SuggestEnabled)
        {
            var error = settings.Enable ? DadataMessages.NoApiKey : DadataMessages.Disabled;
            return Result.Failure<IReadOnlyList<DadataSuggestion>>(error);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, SuggestUrl)
        {
            Content = JsonContent.Create(new { query = address, count = SuggestCount })
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Token {settings.ApiKey.Trim()}");

        var sent = await Send<DadataSuggestionResponse>(request, cancellationToken);
        return sent.IsFailure
            ? Result.Failure<IReadOnlyList<DadataSuggestion>>(sent.Error)
            : Result.Success<IReadOnlyList<DadataSuggestion>>(sent.Value.Suggestions ?? []);
    }

    public async Task<Result<IReadOnlyList<DadataCleanResult>>> Clean(
        string address,
        CancellationToken cancellationToken)
    {
        var settings = await ReadSettings();
        if (!settings.CleanEnabled)
        {
            var error = string.IsNullOrWhiteSpace(settings.ApiKey) && settings.Enable
                ? DadataMessages.NoApiKey
                : DadataMessages.NoSecret;
            return Result.Failure<IReadOnlyList<DadataCleanResult>>(error);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, CleanUrl)
        {
            Content = JsonContent.Create(new[] { address })
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Token {settings.ApiKey.Trim()}");
        request.Headers.TryAddWithoutValidation("X-Secret", settings.Secret.Trim());

        var cleaned = await Send<List<DadataCleanResult>>(request, cancellationToken);
        return cleaned.IsFailure
            ? Result.Failure<IReadOnlyList<DadataCleanResult>>(cleaned.Error)
            : Result.Success<IReadOnlyList<DadataCleanResult>>(cleaned.Value);
    }

    private async Task<DadataSettings> ReadSettings() =>
        (await _parameters.Current()).DadataSettings ?? new();

    private async Task<Result<T>> Send<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = _http.CreateClient(HttpClientName);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Result.Failure<T>(await ErrorText(response, cancellationToken));

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var value = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
            return value == null
                ? Result.Failure<T>(DadataMessages.Unavailable)
                : Result.Success(value);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
            && !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<T>(DadataMessages.Unavailable);
        }
    }

    /// <summary>
    /// Из тела ошибки берётся message. Поля нет — общий текст.
    /// </summary>
    private static async Task<string> ErrorText(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var error = JsonSerializer.Deserialize<DadataErrorResponse>(body);
            if (!string.IsNullOrWhiteSpace(error?.Message))
                return error.Message.Trim();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
        }

        return DadataMessages.Unavailable;
    }
}
