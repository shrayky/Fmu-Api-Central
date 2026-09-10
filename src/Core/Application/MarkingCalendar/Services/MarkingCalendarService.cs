using Application.MarkingCalendar.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using System.Net.Http.Headers;

namespace Application.MarkingCalendar.Services;

[AutoRegisterService]
public class MarkingCalendarService(IHttpClientFactory httpClientFactory) : IMarkingCalendarService
{
    public const string HttpClientName = "ChestnyZnak";

    private const string SourceUrl =
        "https://xn--80ajghhoc2aj1c8b.xn--p1ai/bitrix/services/main/ajax.php?mode=class&c=dev:markingCalendar&action=getSheduleList";

    public async Task<Result<string>> GetSchedule(CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, SourceUrl);
            request.Headers.TryAddWithoutValidation("Referer", "https://xn--80ajghhoc2aj1c8b.xn--p1ai/");
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return Result.Failure<string>("Не удалось загрузить календарь внедрения");

            return Result.Success(body);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }
}
