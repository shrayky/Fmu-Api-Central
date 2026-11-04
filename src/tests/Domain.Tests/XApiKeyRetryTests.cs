using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class XApiKeyRetryTests
{
    /// <summary>
    /// Просроченный срок CDN не запрашиваем снова ближайшие 10 часов.
    /// </summary>
    [Fact]
    public void NextAttempt_некорректный_срок_через_10_часов()
    {
        var now = new DateTime(2026, 11, 4, 18, 0, 0, DateTimeKind.Local);

        var next = XApiKeyRetry.NextAttempt(
            "True API вернул некорректный срок действия x-api-key",
            now);

        Assert.Equal(now.AddHours(10), next);
    }

    /// <summary>
    /// Сбой сети и отсутствие сертификата повторяются на следующем цикле воркера.
    /// </summary>
    [Fact]
    public void NextAttempt_другая_ошибка_без_паузы()
    {
        var now = new DateTime(2026, 11, 4, 18, 0, 0, DateTimeKind.Local);

        var next = XApiKeyRetry.NextAttempt("Ошибка получения x-api-key в true api timeout", now);

        Assert.Null(next);
    }
}
