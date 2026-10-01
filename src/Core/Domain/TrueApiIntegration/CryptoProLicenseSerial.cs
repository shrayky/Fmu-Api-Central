using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration;

public static partial class CryptoProLicenseSerial
{
    [GeneratedRegex("^[0-9A-Za-z]{5}(-[0-9A-Za-z]{5}){4}$")]
    private static partial Regex Pattern();

    public static Result<string> Normalize(string? serial)
    {
        var value = (serial ?? string.Empty).Trim();
        if (!Pattern().IsMatch(value))
            return Result.Failure<string>("Серийный номер лицензии КриптоПро: пять групп по пять символов");

        return Result.Success(value);
    }
}
