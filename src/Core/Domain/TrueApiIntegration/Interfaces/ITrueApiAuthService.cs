using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration.Interfaces;

public interface ITrueApiAuthService
{
    /// <summary>
    /// Получает токен True API: ключ авторизации, подпись КриптоПро, simpleSignIn.
    /// </summary>
    Task<Result<string>> GenerateToken(string inn, string password, string signatureNumber);
}
