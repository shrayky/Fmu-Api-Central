using CSharpFunctionalExtensions;
using Domain.GisMt.Enum;

namespace Domain.GisMt.Interfaces;

/// <summary>
/// Один круг фонового обмена с GisMt и ручной запуск операций.
/// </summary>
public interface IGisMtExchangeService
{
    /// <summary>
    /// Один круг обмена: пакет настроек на GisMt и запись статусов организаций.
    /// </summary>
    Task AutomaticExchange(CancellationToken cancellationToken);

    /// <summary>
    /// Ставит в очередь ручную операцию ГИС МТ для организации.
    /// </summary>
    Task<Result> ManualOperation(string organizationId, GisMtManualOperationKind kind, CancellationToken cancellationToken);
}
