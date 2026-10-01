using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration.Interfaces;

public interface IDigitalSignatureService
{
    /// <summary>
    /// Возвращает действующие сертификаты ЭЦП из хранилища текущего пользователя.
    /// </summary>
    List<DigitalSignature> List();

    /// <summary>
    /// Истёкшие остаются: список выбора их скрывает, оповещение должно их видеть.
    /// </summary>
    List<DigitalSignature> ListIncludingExpired();

    /// <summary>
    /// Ставит zip-контейнер в хранилище ключей текущего пользователя и привязывает сертификат.
    /// Имя папки в архиве — часть имени контейнера, его нельзя переименовывать.
    /// </summary>
    Result Install(Stream archive);
}
