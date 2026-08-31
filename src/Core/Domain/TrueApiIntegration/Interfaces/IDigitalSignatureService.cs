namespace Domain.TrueApiIntegration.Interfaces;

public interface IDigitalSignatureService
{
    /// <summary>
    /// Возвращает действующие сертификаты ЭЦП из хранилища текущего пользователя.
    /// </summary>
    List<DigitalSignature> List();
}
