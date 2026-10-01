using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration.Interfaces;

public interface ITrueApiAuthService
{
    /// <summary>
    /// Получает единый токен UUID: auth/key, подпись КриптоПро, simpleSignIn с unitedToken.
    /// </summary>
    Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber);
}
