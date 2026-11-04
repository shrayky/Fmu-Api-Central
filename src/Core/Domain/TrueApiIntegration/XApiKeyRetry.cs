namespace Domain.TrueApiIntegration;

public static class XApiKeyRetry
{
    public const string ExpiredLifetimeError = "True API вернул некорректный срок действия x-api-key";

    public static DateTime? NextAttempt(string error, DateTime now)
        => error == ExpiredLifetimeError ? now.AddHours(10) : null;
}
