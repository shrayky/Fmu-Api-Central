using Microsoft.AspNetCore.Http;

namespace Application.Instance.Services;

public static class CheckerDistributionLimits
{
    public const string Busy = "Сборка дистрибутива уже выполняется";

    public static int FailureStatusCode(string error)
        => string.Equals(error, Busy, StringComparison.Ordinal)
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status400BadRequest;
}
