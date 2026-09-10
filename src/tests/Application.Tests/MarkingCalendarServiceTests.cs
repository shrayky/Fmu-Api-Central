using Application.MarkingCalendar.Services;
using System.Net;

namespace Application.Tests;

public class MarkingCalendarServiceTests
{
    /// <summary>
    /// Прокси отдаёт тело ответа Честного знака без изменений.
    /// </summary>
    [Fact]
    public async Task GetSchedule_возвращает_json_источника()
    {
        const string json = """{"status":"success","data":{"items":[]}}""";
        var sut = CreateSut(_ => Ok(json));

        var result = await sut.GetSchedule(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(json, result.Value);
    }

    /// <summary>
    /// Сайт Честный знак недоступен — UI показывает одно и то же сообщение.
    /// </summary>
    [Fact]
    public async Task GetSchedule_ошибка_источника()
    {
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        var result = await sut.GetSchedule(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Не удалось загрузить календарь внедрения", result.Error);
    }

    /// <summary>
    /// Сетевой сбой не должен ронять запрос — в UI уходит текст исключения.
    /// </summary>
    [Fact]
    public async Task GetSchedule_ошибка_сети()
    {
        var sut = CreateSut(_ => throw new HttpRequestException("connection refused"));

        var result = await sut.GetSchedule(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("connection refused", result.Error);
    }

    private static MarkingCalendarService CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new StubHttpClientFactory(new StubHandler(respond)));

    private static HttpResponseMessage Ok(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
