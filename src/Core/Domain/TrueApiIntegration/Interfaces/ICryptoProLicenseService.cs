using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration.Interfaces;

public interface ICryptoProLicenseService
{
    /// <summary>
    /// Текст cpconfig -license -view. Лицензия одна на машину, где запущена служба.
    /// </summary>
    Result<string> View();

    Result<string> Set(string serial);
}
