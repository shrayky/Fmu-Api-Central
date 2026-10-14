using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration.Interfaces;

public interface ITrueApiAuthService
{
    /// <summary>
    /// Получает единый токен UUID: auth/key, подпись КриптоПро, simpleSignIn с unitedToken.
    /// </summary>
    Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber);

    /// <summary>
    /// Ключ CDN: присоединённая подпись ИНН и POST /auth/permissive-access на cdn.crpt.ru.
    /// </summary>
    Task<Result<TrueApiSession>> GenerateXApiKey(string inn, string password, string signatureNumber);
}
