using CSharpFunctionalExtensions;
using Domain.GisMt.Dto;
using Domain.GisMt.Enum;

namespace Domain.GisMt.Interfaces;

/// <summary>
/// HTTP-клиент Central → GisMt.
/// </summary>
public interface IGisMtClient
{
    /// <summary>
    /// Отправляет пакет настроек на GisMt.
    /// </summary>
    Task<Result> PutExchangeConfiguration(string serviceUrl, GisMtConfigurationPacket packet, CancellationToken cancellationToken);

    /// <summary>
    /// Читает статусы организаций с GisMt.
    /// </summary>
    Task<Result<GisMtStatusResponse>> FmuApiGisMtStatus(string serviceUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Отправляет ручную операцию ГИС МТ.
    /// </summary>
    Task<Result> Operation(string serviceUrl, GisMtManualOperationKind kind, GisMtManualOperationRequest request, CancellationToken cancellationToken);
}
